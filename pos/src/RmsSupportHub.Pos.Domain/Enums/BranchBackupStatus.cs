namespace RmsSupportHub.Pos.Domain.Enums;

public enum BranchBackupStatus
{
    Pending,
    Triggered,
    Waiting,
    ZipDetected,
    Validating,
    Ready,
    Downloading,
    Downloaded,
    Failed,
    TimedOut,
    Cancelled
}
