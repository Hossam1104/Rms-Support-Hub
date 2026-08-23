namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public enum DatabaseHealthViewState
{
    Loading,
    Healthy,
    Degraded,
    Unavailable,
    TimedOut,
    ProtocolMismatch,
    InvalidResponse,
    SecurityVerificationFailed,
    Unknown,
    UnknownError,
    Cancelled
}
