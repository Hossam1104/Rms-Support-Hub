namespace RmsSupportHub.Pos.Contracts.V1.Rms;

public sealed record RmsDatabaseOperationDto(
    string OperationId,
    RmsDatabaseTarget Target,
    string DatabaseDisplayName,
    RmsDatabaseOperationKind Operation,
    RmsDatabaseOperationState State,
    RmsDatabaseOperationOutcome Outcome,
    int ProgressPercent,
    string Stage,
    string Detail,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    RmsDatabaseArtifactDto? Artifact,
    bool DestructiveAttempted,
    bool RecoveryRequired,
    IReadOnlyList<string> Warnings,
    string? ErrorCode,
    string CorrelationId);
