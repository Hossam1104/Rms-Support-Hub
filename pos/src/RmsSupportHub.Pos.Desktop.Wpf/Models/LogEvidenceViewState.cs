namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public enum LogEvidenceViewState
{
    Loading,
    Healthy,
    Degraded,
    Unavailable,
    TimedOut,
    ProtocolMismatch,
    SecurityVerificationFailed,
    InvalidResponse,
    Unknown,
    UnknownError,
    Cancelled
}
