using System.Diagnostics;
using System.Text;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Diagnostics;

/// <summary>
/// Real process boundary for the typed console seam. The Agent never passes shell text and never
/// inherits the host environment; callers must supply a manifest-resolved executable and root.
/// </summary>
public sealed class ConstrainedDiagnosticProcessRunner : IConstrainedDiagnosticProcessRunner
{
    private const int MaxWallSeconds = 120;
    private const int MaxOutputBytes = 256 * 1024;
    private const int MaxOutputLines = 4096;

    public async Task<DiagnosticConsoleProcessResult> RunAsync(
        DiagnosticConsoleLaunchSpec launchSpec,
        TimeSpan wallTimeLimit,
        int outputByteLimit,
        int outputLineLimit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(launchSpec);
        if (!ValidateBounds(launchSpec, wallTimeLimit, outputByteLimit, outputLineLimit))
        {
            return new(DiagnosticConsoleProcessState.Unknown, null, string.Empty, string.Empty, 0, 0, 0, 0, false, false,
                "The Agent rejected the process boundary limits.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = launchSpec.ExecutablePath,
            WorkingDirectory = launchSpec.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in launchSpec.Arguments) startInfo.ArgumentList.Add(argument);
        startInfo.Environment.Clear();

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
            {
                return new(DiagnosticConsoleProcessState.Unknown, null, string.Empty, string.Empty, 0, 0, 0, 0, false, false,
                    "The fixed diagnostic process did not start.");
            }

            var stdoutTask = ReadBoundedAsync(process.StandardOutput, outputByteLimit, outputLineLimit, cancellationToken);
            var stderrTask = ReadBoundedAsync(process.StandardError, outputByteLimit, outputLineLimit, cancellationToken);
            var waitTask = process.WaitForExitAsync(cancellationToken);
            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var timeoutTask = Task.Delay(wallTimeLimit, timeoutCancellation.Token);
            var completed = await Task.WhenAny(waitTask, timeoutTask).ConfigureAwait(false);
            DiagnosticConsoleProcessState state;

            if (completed == waitTask)
            {
                // Stop the delay as soon as the process has a terminal result. This avoids leaving
                // a live timer behind for every successful diagnostic invocation.
                timeoutCancellation.Cancel();
                await waitTask.ConfigureAwait(false);
                state = process.ExitCode == 0 ? DiagnosticConsoleProcessState.Succeeded : DiagnosticConsoleProcessState.Failed;
            }
            else
            {
                state = cancellationToken.IsCancellationRequested
                    ? DiagnosticConsoleProcessState.Cancelled
                    : DiagnosticConsoleProcessState.TimedOut;
                TryKillTree(process);
                try { await waitTask.ConfigureAwait(false); } catch { }
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            return new(
                state,
                process.HasExited ? process.ExitCode : null,
                stdout.Text,
                stderr.Text,
                stdout.Bytes,
                stderr.Bytes,
                stdout.Lines,
                stderr.Lines,
                stdout.Truncated || stderr.Truncated,
                false,
                state switch
                {
                    DiagnosticConsoleProcessState.Succeeded => "The fixed diagnostic process completed.",
                    DiagnosticConsoleProcessState.Failed => "The fixed diagnostic process returned a failure exit code.",
                    DiagnosticConsoleProcessState.TimedOut => "The fixed diagnostic process exceeded its wall-time limit and was terminated.",
                    DiagnosticConsoleProcessState.Cancelled => "The fixed diagnostic process was cancelled and terminated.",
                    _ => "The fixed diagnostic process outcome is unknown."
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKillTree(process);
            return new(DiagnosticConsoleProcessState.Cancelled, null, string.Empty, string.Empty, 0, 0, 0, 0, false, false,
                "The fixed diagnostic process was cancelled before its outcome was confirmed.");
        }
        catch
        {
            TryKillTree(process);
            return new(DiagnosticConsoleProcessState.Unknown, null, string.Empty, string.Empty, 0, 0, 0, 0, false, false,
                "The fixed diagnostic process could not be observed safely.");
        }
    }

    private static bool ValidateBounds(DiagnosticConsoleLaunchSpec spec, TimeSpan wall, int bytes, int lines) =>
        Path.IsPathFullyQualified(spec.ExecutablePath)
        && Path.IsPathFullyQualified(spec.WorkingDirectory)
        && IsSafeExecutable(spec.ExecutablePath, spec.WorkingDirectory)
        && spec.Arguments.Count <= 16
        && spec.Arguments.All(argument => argument.Length <= 256 && argument.All(character => !char.IsControl(character)))
        && spec.Environment.Count == 0
        && wall > TimeSpan.Zero && wall <= TimeSpan.FromSeconds(MaxWallSeconds)
        && bytes is > 0 and <= MaxOutputBytes
        && lines is > 0 and <= MaxOutputLines;

    private static bool IsSafeExecutable(string executablePath, string workingDirectory)
    {
        try
        {
            if (!Directory.Exists(workingDirectory)
                || !File.Exists(executablePath)
                || File.GetAttributes(workingDirectory).HasFlag(FileAttributes.ReparsePoint)
                || File.GetAttributes(executablePath).HasFlag(FileAttributes.ReparsePoint)) return false;

            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workingDirectory));
            var executable = Path.GetFullPath(executablePath);
            if (string.Equals(root, executable, StringComparison.OrdinalIgnoreCase)) return false;
            var relative = Path.GetRelativePath(root, executable);
            return !Path.IsPathFullyQualified(relative)
                && !relative.Equals("..", StringComparison.Ordinal)
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<BoundedText> ReadBoundedAsync(
        StreamReader reader,
        int byteLimit,
        int lineLimit,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var builder = new StringBuilder();
        var bytes = 0;
        var lines = 0;
        var truncated = false;
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            var chunkBytes = Encoding.UTF8.GetByteCount(buffer, 0, count);
            var nextBytes = bytes + chunkBytes;
            var nextLines = lines + buffer.AsSpan(0, count).Count('\n');
            if (nextBytes <= byteLimit && nextLines <= lineLimit)
            {
                builder.Append(buffer, 0, count);
                bytes = nextBytes;
                lines = nextLines;
            }
            else
            {
                truncated = true;
                bytes = Math.Min(byteLimit, nextBytes);
                lines = Math.Min(lineLimit, nextLines);
            }
        }

        return new(builder.ToString(), bytes, lines, truncated);
    }

    private static void TryKillTree(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch { }
    }

    private sealed record BoundedText(string Text, int Bytes, int Lines, bool Truncated);
}
