using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Diagnostics;

/// <summary>
/// Reads aggregate evidence from fixed RMS roots. Reparse points and raw names are discarded;
/// insurance data is projected through the same aggregate-only path.
/// </summary>
public sealed class WindowsRmsFixedHealthReader(
    RmsFixedHealthOptions options,
    TimeProvider timeProvider) : IRmsFixedHealthReader
{
    public Task<IReadOnlyList<RmsFixedRootHealth>> ReadRootsAsync(CancellationToken cancellationToken = default)
    {
        options.Validate();
        var results = options.Definitions
            .Select(definition => ReadRoot(definition, cancellationToken))
            .ToArray();
        return Task.FromResult<IReadOnlyList<RmsFixedRootHealth>>(results);
    }

    public async Task<RmsUpdateHealth> ReadUpdateHealthAsync(CancellationToken cancellationToken = default)
    {
        var roots = await ReadRootsAsync(cancellationToken).ConfigureAwait(false);
        var setup = Find(roots, "rms-setup");
        var downloads = Find(roots, "rms-downloads");
        var repository = Find(roots, "rms-release-repository");
        var release = ReadReleaseNumber(options.SetupRoot);
        var packageState = downloads.State == RmsFixedRootState.Inaccessible
            || repository.State == RmsFixedRootState.Inaccessible
            ? "unavailable"
            : downloads.Exists && repository.Exists ? "available" : "not-installed";
        return new(
            setup,
            downloads,
            repository,
            release.Value,
            release.Available,
            packageState,
            release.Available ? "Product Release was read from the fixed release file." : "Product Release is not available from the fixed release file.");
    }

    public async Task<RmsInsuranceAttachmentHealth> ReadInsuranceAttachmentHealthAsync(CancellationToken cancellationToken = default)
    {
        var root = Find(await ReadRootsAsync(cancellationToken).ConfigureAwait(false), "insurance-attachments");
        return new(root, root.FileCount, root.TotalBytes, root.OldestFileUtc, root.NewestFileUtc,
            root.State == RmsFixedRootState.Healthy
                ? "Only aggregate attachment storage metadata is exposed; contents and names are never returned."
                : "Attachment storage aggregate metadata is unavailable.");
    }

    private RmsFixedRootHealth ReadRoot(RmsFixedRootDefinition definition, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = timeProvider.GetUtcNow();
        var rootPath = definition.RootPath;
        try
        {
            if (!Directory.Exists(rootPath))
            {
                return new(definition.RootId, definition.DisplayName, RmsFixedRootState.Missing, false, false, 0, 0, null, null, null, null, "The fixed RMS evidence root is not present.");
            }

            if (IsReparse(rootPath))
            {
                return new(definition.RootId, definition.DisplayName, RmsFixedRootState.Inaccessible, true, false, 0, 0, null, null, null, null, "The fixed RMS evidence root is a reparse point and was not read.");
            }

            var aggregate = Aggregate(rootPath, cancellationToken);
            var stale = aggregate.NewestFileUtc is { } newest
                && now - newest > TimeSpan.FromDays(14)
                && definition.RootId is "branch-logs" or "cashier-logs" or "rms-downloads";
            var state = stale ? RmsFixedRootState.Stale : RmsFixedRootState.Healthy;
            var detail = stale ? "The fixed RMS evidence root has no recent files." : "The fixed RMS evidence root was read within the bounded file and byte limits.";
            return new(definition.RootId, definition.DisplayName, state, true, true, aggregate.FileCount, aggregate.TotalBytes, aggregate.OldestFileUtc, aggregate.NewestFileUtc, DriveFree(rootPath), DriveCapacity(rootPath), detail);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new(definition.RootId, definition.DisplayName, RmsFixedRootState.Inaccessible, true, false, 0, 0, null, null, null, null, "The fixed RMS evidence root could not be read safely.");
        }
    }

    private AggregateResult Aggregate(string rootPath, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>([rootPath]);
        var fileCount = 0;
        var totalBytes = 0L;
        DateTimeOffset? oldest = null;
        DateTimeOffset? newest = null;
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            if (IsReparse(current)) continue;
            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if (pending.Count < options.MaximumFilesPerRoot && !IsReparse(directory)) pending.Push(directory);
            }

            foreach (var file in Directory.EnumerateFiles(current))
            {
                if (fileCount >= options.MaximumFilesPerRoot) return new(fileCount, totalBytes, oldest, newest);
                if (IsReparse(file)) continue;
                var info = new FileInfo(file);
                var length = Math.Max(0, info.Length);
                totalBytes = totalBytes > options.MaximumBytesPerRoot - Math.Min(length, options.MaximumBytesPerRoot)
                    ? options.MaximumBytesPerRoot
                    : Math.Min(options.MaximumBytesPerRoot, totalBytes + length);
                var modified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
                oldest = oldest is null || modified < oldest ? modified : oldest;
                newest = newest is null || modified > newest ? modified : newest;
                fileCount++;
            }
        }

        return new(fileCount, totalBytes, oldest, newest);
    }

    private (bool Available, string? Value) ReadReleaseNumber(string setupRoot)
    {
        try
        {
            var path = Path.Combine(setupRoot, "ReleaseNumber.txt");
            if (!File.Exists(path) || IsReparse(path) || new FileInfo(path).Length > 256) return (false, null);
            var value = File.ReadAllText(path).Trim();
            if (string.IsNullOrWhiteSpace(value) || value.Any(character => char.IsControl(character) || character is '\\' or '/' or ':' or '?' or '#') || value.Length > 64) return (false, null);
            return (true, value);
        }
        catch { return (false, null); }
    }

    private static RmsFixedRootHealth Find(IReadOnlyList<RmsFixedRootHealth> roots, string rootId) =>
        roots.First(root => string.Equals(root.RootId, rootId, StringComparison.Ordinal));

    private static bool IsReparse(string path) =>
        (Directory.Exists(path) || File.Exists(path))
        && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);

    private static long? DriveFree(string path)
    {
        try { return new DriveInfo(Path.GetPathRoot(path)!).AvailableFreeSpace; } catch { return null; }
    }

    private static long? DriveCapacity(string path)
    {
        try { return new DriveInfo(Path.GetPathRoot(path)!).TotalSize; } catch { return null; }
    }

    private sealed record AggregateResult(int FileCount, long TotalBytes, DateTimeOffset? OldestFileUtc, DateTimeOffset? NewestFileUtc);
}
