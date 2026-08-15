using RmsSupportHub.Pos.Contracts.V1.Packages;

namespace RmsSupportHub.Pos.Contracts.V1.Repair;

public enum RepairOperationKindDto
{
    Install,
    Upgrade,
    Uninstall,
    Rollback,
    Repair,
    Guided
}

public enum RepairOperationStateDto
{
    NotAttempted,
    Preview,
    Accepted,
    Running,
    Completed,
    Failed,
    Partial,
    RollbackSucceeded,
    RollbackFailed,
    RecoveryRequired,
    OutcomeUnknown
}

public enum GuidedRepairStepStateDto
{
    Pending,
    Ready,
    Confirmed,
    Completed,
    Failed,
    Blocked,
    RecoveryRequired
}

public sealed record RepairPreviewDto(
    string PreviewId,
    RepairOperationKindDto Operation,
    bool Ready,
    AgentPackageVerificationStateDto PackageVerification,
    SafetySnapshotReferenceDto Snapshot,
    IReadOnlyList<string> Effects,
    IReadOnlyList<string> Blockers,
    string ConfirmationPhrase,
    DateTimeOffset ExpiresAtUtc);

public sealed record SafetySnapshotReferenceDto(
    string? SnapshotId,
    string State,
    bool Verified,
    DateTimeOffset? ExpiresAtUtc,
    string Detail);

public sealed record RepairExecuteRequestDto(
    string PreviewId,
    string TypedConfirmation,
    string IdempotencyKey,
    string? SnapshotId = null);

public sealed record RepairOperationDto(
    string OperationId,
    RepairOperationKindDto Operation,
    RepairOperationStateDto State,
    RepairOperationStateDto Outcome,
    int ProgressPercent,
    string Stage,
    string Detail,
    bool RollbackAttempted,
    bool RollbackSucceeded,
    bool RecoveryRequired,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string CorrelationId);

public sealed record GuidedRepairDto(
    string GuidedRepairId,
    RepairOperationStateDto State,
    IReadOnlyList<GuidedRepairStepDto> Steps,
    string Detail,
    DateTimeOffset ExpiresAtUtc);

public sealed record GuidedRepairStepDto(
    string StepId,
    string Title,
    string Description,
    GuidedRepairStepStateDto State,
    bool RequiresConfirmation,
    string? ConfirmationPhrase,
    string? FailureCode);

public sealed record GuidedRepairStepRequestDto(
    string GuidedRepairId,
    string StepId,
    string TypedConfirmation,
    string IdempotencyKey);
