namespace RmsSupportHub.Pos.Domain.Models;

public enum DiagnosticConsoleTarget
{
    BranchServerApi,
    CashierServerApi,
    ServiceManager,
    CashierUi
}

public enum DiagnosticConsoleRunState
{
    NotAttempted,
    Accepted,
    Running,
    Succeeded,
    Failed,
    TimedOut,
    Cancelled,
    Partial,
    OutcomeUnknown
}

public enum DiagnosticConsoleProcessState
{
    Succeeded,
    Failed,
    TimedOut,
    Cancelled,
    Unknown
}

/// <summary>
/// Canonical manifest entry. Only the Agent constructs a ProcessLaunchSpec from this data.
/// </summary>
public sealed record DiagnosticConsoleExecutableDefinition(
    DiagnosticConsoleTarget Target,
    string DisplayName,
    string ExecutableFileName,
    string WorkingDirectoryKey,
    IReadOnlyList<string> ArgumentTemplate,
    TimeSpan MaxWallTime,
    int MaxOutputBytes,
    int MaxOutputLines);

public sealed record DiagnosticConsoleLaunchSpec(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment);

public sealed record DiagnosticConsoleProcessResult(
    DiagnosticConsoleProcessState State,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    int StandardOutputBytes,
    int StandardErrorBytes,
    int StandardOutputLines,
    int StandardErrorLines,
    bool OutputTruncated,
    bool RedactionApplied,
    string Detail);

public interface IDiagnosticConsoleManifest
{
    bool TryGet(DiagnosticConsoleTarget target, out DiagnosticConsoleExecutableDefinition definition);
}

public interface IConstrainedDiagnosticProcessRunner
{
    Task<DiagnosticConsoleProcessResult> RunAsync(
        DiagnosticConsoleLaunchSpec launchSpec,
        TimeSpan wallTimeLimit,
        int outputByteLimit,
        int outputLineLimit,
        CancellationToken cancellationToken = default);
}

public interface IDiagnosticConsoleOutputRedactor
{
    string Redact(string value);
}
