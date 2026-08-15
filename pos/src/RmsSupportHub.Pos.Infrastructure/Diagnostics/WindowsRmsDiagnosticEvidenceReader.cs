using System.Diagnostics.Eventing.Reader;
using System.Text;
using System.Text.RegularExpressions;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Diagnostics;

/// <summary>
/// Reads only the fixed RMS log roots and the allow-listed Windows event sources relevant to
/// service failures. All reads are bounded, best-effort, and redacted before returning.
/// </summary>
public sealed class WindowsRmsDiagnosticEvidenceReader(
    RmsDiagnosticEvidenceOptions options,
    TimeProvider timeProvider) : IRmsDiagnosticEvidenceReader
{
    private static readonly IReadOnlyDictionary<string, EvidenceSource> Sources =
        new Dictionary<string, EvidenceSource>(StringComparer.OrdinalIgnoreCase)
        {
            [RmsServiceCatalog.BranchServiceName] = new("Branch RMS log", "Branch", options => options.BranchLogRoot),
            [RmsServiceCatalog.CashierServiceName] = new("Cashier RMS log", "Cashier", options => options.CashierLogRoot),
            [RmsServiceCatalog.ServicesManagerServiceName] = new("RMS Services Manager log", "Services Manager", options => options.ServicesManagerLogRoot)
        };

    public async Task<RmsDiagnosticEvidenceReadResult> ReadAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        options.Validate();
        _ = timeProvider.GetUtcNow();
        if (!Sources.TryGetValue(serviceName, out var source))
        {
            return new([], ["The requested diagnostic source is not allow-listed."]);
        }

        var records = new List<RmsDiagnosticEvidenceRecord>();
        var unknownReasons = new List<string>();
        await ReadLogFilesAsync(source, records, unknownReasons, cancellationToken).ConfigureAwait(false);
        ReadWindowsEvents(source, records, unknownReasons, cancellationToken);

        return new(
            records.OrderByDescending(record => record.AtUtc ?? DateTimeOffset.MinValue)
                .Take(options.MaxEventRecords)
                .ToArray(),
            unknownReasons.Distinct(StringComparer.Ordinal).Take(8).ToArray());
    }

    private async Task ReadLogFilesAsync(
        EvidenceSource source,
        ICollection<RmsDiagnosticEvidenceRecord> records,
        ICollection<string> unknownReasons,
        CancellationToken cancellationToken)
    {
        string[] files;
        var nowUtc = timeProvider.GetUtcNow();
        try
        {
            if (!TryGetSafeRoot(source.Root(options), out var root))
            {
                unknownReasons.Add($"{source.DisplayName} log root is unavailable.");
                return;
            }

            files = Directory.EnumerateFiles(root, "*.log", SearchOption.TopDirectoryOnly)
                .Select(path => TryGetLogMetadata(path))
                .Where(candidate => candidate is not null
                    && !candidate.Value.IsReparsePoint
                    && nowUtc - candidate.Value.LastWriteUtc <= options.EventWindow
                    && candidate.Value.LastWriteUtc <= nowUtc.AddMinutes(5))
                .OrderByDescending(candidate => candidate!.Value.LastWriteUtc)
                .Take(options.MaxLogFiles)
                .Select(candidate => candidate!.Value.Path)
                .ToArray();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            unknownReasons.Add($"{source.DisplayName} logs could not be enumerated.");
            return;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ReadOneLogAsync(source, file, records, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                unknownReasons.Add($"A {source.DisplayName} log file could not be read.");
            }
        }
    }

    private static (string Path, DateTimeOffset LastWriteUtc, bool IsReparsePoint)? TryGetLogMetadata(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (
                path,
                new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero),
                attributes.HasFlag(FileAttributes.ReparsePoint));
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetSafeRoot(string configuredRoot, out string root)
    {
        root = string.Empty;
        try
        {
            root = Path.GetFullPath(configuredRoot);
            var current = new DirectoryInfo(root);
            if (!current.Exists || current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                root = string.Empty;
                return false;
            }

            for (var directory = current.Parent; directory is not null; directory = directory.Parent)
            {
                if (directory.Exists && directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    root = string.Empty;
                    return false;
                }
            }

            return true;
        }
        catch
        {
            root = string.Empty;
            return false;
        }
    }

    private async Task ReadOneLogAsync(
        EvidenceSource source,
        string file,
        ICollection<RmsDiagnosticEvidenceRecord> records,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            file,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            16 * 1024,
            FileOptions.SequentialScan);
        if (stream.Length > options.MaxBytesPerLog)
        {
            stream.Seek(-options.MaxBytesPerLog, SeekOrigin.End);
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lines = new List<string>();
        while (lines.Count < options.MaxLinesPerLog && await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            lines.Add(line);
        }

        var logTime = new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero);
        foreach (var line in lines.Where(IsFailureLine).Take(options.MaxEventRecords))
        {
            var frames = lines.SkipWhile(candidate => !candidate.Contains(" at ", StringComparison.OrdinalIgnoreCase))
                .Take(12);
            records.Add(new(
                source.DisplayName,
                logTime,
                line.Contains("error", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("exception", StringComparison.OrdinalIgnoreCase)
                    ? RmsEvidenceSeverity.Error
                    : RmsEvidenceSeverity.Warning,
                DiagnosticRedactor.RedactSummary(line),
                DiagnosticRedactor.ExceptionType(line),
                DiagnosticRedactor.StackFrames(frames),
                null));
        }
    }

    private void ReadWindowsEvents(
        EvidenceSource source,
        ICollection<RmsDiagnosticEvidenceRecord> records,
        ICollection<string> unknownReasons,
        CancellationToken cancellationToken)
    {
        var eventSources = source.Name == "Branch" || source.Name == "Cashier" || source.Name == "Services Manager"
            ? new[]
            {
                new EventSourceDefinition("System", "Service Control Manager", new[] { 7000, 7001, 7009, 7031, 7034 }),
                new EventSourceDefinition("Application", ".NET Runtime", new[] { 1026 }),
                new EventSourceDefinition("Application", "Application Error", new[] { 1000 }),
                new EventSourceDefinition("Application", "Windows Error Reporting", new[] { 1001 })
            }
            : [];

        foreach (var definition in eventSources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var query = new EventLogQuery(
                    definition.LogName,
                    PathType.LogName,
                    $"*[System[TimeCreated[timediff(@SystemTime) <= {Math.Max(1, (int)options.EventWindow.TotalMilliseconds)}]]]" )
                {
                    ReverseDirection = true
                };
                using var reader = new EventLogReader(query);
                for (var index = 0; index < options.MaxEventRecords && reader.ReadEvent() is { } record; index++)
                {
                    using (record)
                    {
                        if (!string.Equals(record.ProviderName, definition.Provider, StringComparison.OrdinalIgnoreCase)
                            || !definition.EventIds.Contains(record.Id))
                        {
                            continue;
                        }

                        var message = string.Empty;
                        try { message = record.FormatDescription() ?? string.Empty; } catch { }
                        records.Add(new(
                            $"Windows {definition.LogName} event",
                            record.TimeCreated,
                            RmsEvidenceSeverity.Error,
                            DiagnosticRedactor.RedactSummary(
                                string.IsNullOrWhiteSpace(message)
                                    ? $"{definition.Provider} event {record.Id} was recorded."
                                    : message),
                            DiagnosticRedactor.ExceptionType(message),
                            DiagnosticRedactor.StackFrames(message.Split('\n')),
                            record.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
            }
            catch (EventLogNotFoundException)
            {
                unknownReasons.Add($"The {definition.LogName} event channel is unavailable.");
            }
            catch (UnauthorizedAccessException)
            {
                unknownReasons.Add($"The {definition.LogName} event channel could not be read.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                unknownReasons.Add($"A bounded {definition.LogName} event read was unavailable.");
            }
        }
    }

    private static bool IsFailureLine(string line) =>
        line.Contains("error", StringComparison.OrdinalIgnoreCase)
        || line.Contains("exception", StringComparison.OrdinalIgnoreCase)
        || line.Contains("failed", StringComparison.OrdinalIgnoreCase)
        || line.Contains("fatal", StringComparison.OrdinalIgnoreCase)
        || line.Contains("stack trace", StringComparison.OrdinalIgnoreCase);

    private sealed record EvidenceSource(string DisplayName, string Name, Func<RmsDiagnosticEvidenceOptions, string> Root);

    private sealed record EventSourceDefinition(string LogName, string Provider, IReadOnlySet<int> EventIds)
    {
        public EventSourceDefinition(string logName, string provider, IReadOnlyCollection<int> eventIds)
            : this(logName, provider, eventIds.ToHashSet()) { }
    }
}
