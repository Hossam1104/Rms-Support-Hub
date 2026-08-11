namespace RmsSupportHub.Pos.Contracts.V1.Downloader;

/// <summary>Safe browser-facing state for one downloader branch; it contains no path or exception text.</summary>
public enum DownloaderBranchState
{
    Pending,
    Triggered,
    Waiting,
    Detected,
    Validating,
    Ready,
    Downloading,
    Completed,
    TimedOut,
    Cancelled,
    Failed
}
