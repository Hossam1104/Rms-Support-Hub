namespace RmsSupportHub.Pos.Contracts.V1.Maintenance;

public sealed record MaintenanceItemOutcomeDto(
    string TargetId,
    string Kind,
    MaintenanceItemState State,
    bool Attempted,
    bool Completed,
    bool ResidueUncertain,
    string? FailureCode,
    string? RecoveryGuidance);
