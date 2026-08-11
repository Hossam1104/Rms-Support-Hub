namespace RmsSupportHub.Pos.Contracts.V1.Maintenance;

public enum MaintenanceItemState
{
    NotAttempted,
    AlreadyAbsent,
    Completed,
    Rejected,
    Failed,
    RecoveryRequired,
}
