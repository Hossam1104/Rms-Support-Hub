namespace RmsSupportHub.Pos.Contracts.V1.Console;

public enum DiagnosticConsoleRunStateDto
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

public enum DiagnosticConsoleTargetDto
{
    BranchServerApi,
    CashierServerApi,
    ServiceManager,
    CashierUi
}

public sealed record DiagnosticConsolePreviewDto(
    string PreviewId,
    DiagnosticConsoleTargetDto Target,
    bool Ready,
    string DisplayName,
    DiagnosticConsoleRunStateDto State,
    int MaxWallTimeSeconds,
    int MaxOutputBytes,
    int MaxOutputLines,
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<string> Blockers,
    string ConfirmationPhrase,
    DateTimeOffset ExpiresAtUtc);

/// <summary>Typed start input. It contains no command, executable, path, shell, or arguments.</summary>
public sealed record DiagnosticConsoleStartRequestDto(
    string PreviewId,
    string TypedConfirmation,
    string IdempotencyKey);

public sealed record DiagnosticConsoleRunDto(
    string OperationId,
    DiagnosticConsoleTargetDto Target,
    DiagnosticConsoleRunStateDto State,
    DiagnosticConsoleRunStateDto Outcome,
    int ProgressPercent,
    string Stage,
    string Detail,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DiagnosticConsoleResultDto? Result,
    string? ErrorCode,
    string CorrelationId);

/// <summary>Result metadata contains only bounded counts and opaque authenticated artifact IDs.</summary>
public sealed record DiagnosticConsoleResultDto(
    string? StdoutArtifactId,
    string? StderrArtifactId,
    int StdoutBytes,
    int StderrBytes,
    int StdoutLines,
    int StderrLines,
    bool OutputTruncated,
    bool RedactionApplied,
    int? ExitCode,
    string Detail);
