namespace RmsSupportHub.Pos.Contracts.V1.Downloader;

/// <summary>Safe lifecycle state for a server-owned downloader operation.</summary>
public enum DownloaderOperationStateDto
{
    NotAttempted,
    Accepted,
    Running,
    Completed,
    Failed,
    OutcomeUnknown
}
