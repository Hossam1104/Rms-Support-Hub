namespace RmsSupportHub.Pos.Contracts.V1.Maintenance;

/// <summary>
/// Principal-scoped cleanup or branch-reset progress and outcome. Target IDs are logical and no
/// raw path, service name, SQL, credential, or exception text is returned.
/// </summary>
public sealed record MaintenanceOperationDto(
    string OperationId,
    string Mode,
    MaintenanceOperationStateDto State,
    MaintenanceOperationStateDto Outcome,
    int ProgressPercent,
    string Stage,
    string Detail,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    MaintenanceOperationOutcomeDto? MaintenanceOutcome,
    string? ErrorCode,
    string CorrelationId);
