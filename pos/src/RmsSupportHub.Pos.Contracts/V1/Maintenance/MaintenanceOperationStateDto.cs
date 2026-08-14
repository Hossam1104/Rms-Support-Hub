namespace RmsSupportHub.Pos.Contracts.V1.Maintenance;

/// <summary>Safe lifecycle state for a server-owned maintenance operation.</summary>
public enum MaintenanceOperationStateDto
{
    NotAttempted,
    Accepted,
    Running,
    Completed,
    Failed,
    OutcomeUnknown
}
