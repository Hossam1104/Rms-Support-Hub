namespace RmsSupportHub.Pos.Contracts.V1.Maintenance;

/// <summary>
/// Safe destructive truth retained with the operation. Target IDs are logical and do not contain
/// absolute paths, connection strings, credentials, or exception text.
/// </summary>
public sealed record MaintenanceOperationOutcomeDto(
    bool DestructiveAttempted,
    bool RecoveryRequired,
    IReadOnlyList<MaintenanceItemOutcomeDto> Items,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> RecoveryGuidance);
