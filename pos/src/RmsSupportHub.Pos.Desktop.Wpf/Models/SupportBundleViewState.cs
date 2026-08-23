namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public enum SupportBundleViewState
{
    Idle,
    Generating,
    Succeeded,
    Unavailable,
    Unauthorized,
    TimedOut,
    ProtocolMismatch,
    SecurityVerificationFailed,
    InvalidResponse,
    AuditUnavailable,
    Failed,
    Cancelled
}
