namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public enum HealthViewState
{
    Loading,
    Connected,
    Unavailable,
    TimedOut,
    ProtocolMismatch,
    InvalidResponse,
    SecurityVerificationFailed,
    UnknownError,
    Cancelled
}
