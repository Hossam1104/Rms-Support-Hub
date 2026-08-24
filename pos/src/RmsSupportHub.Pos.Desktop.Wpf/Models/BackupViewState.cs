namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public enum BackupViewState
{
    Idle,
    Loading,
    Creating,
    Succeeded,
    Unavailable,
    Unauthorized,
    TimedOut,
    ProtocolMismatch,
    SecurityVerificationFailed,
    InvalidResponse,
    AuditUnavailable,
    OperationInProgress,
    Cancelled,
    Failed
}

public enum ArtifactExportViewState
{
    Idle,
    SelectingDestination,
    Exporting,
    Succeeded,
    DestinationExists,
    DestinationRejected,
    ArtifactExpired,
    ArtifactNotFound,
    ChecksumMismatch,
    Unauthorized,
    Cancelled,
    Failed
}
