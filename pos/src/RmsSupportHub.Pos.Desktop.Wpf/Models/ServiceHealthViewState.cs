namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public enum ServiceHealthViewState
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
