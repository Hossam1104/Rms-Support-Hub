namespace RmsSupportHub.Pos.Domain.Models;

public enum DownloaderTriggerState
{
    NotAttempted,
    Failed,
    Accepted,
    OutcomeUnknown,
}

/// <summary>
/// Sanitized result from the remote backup-trigger seam. It contains only lifecycle truth and a
/// stable failure code; transport details, URLs, paths, credentials, and exception text stay in
/// Infrastructure.
/// </summary>
public sealed record DownloaderTriggerResult(
    DownloaderTriggerState State,
    string? FailureCode = null);

/// <summary>
/// Application-owned downloader lifecycle truth. The trigger milestone remains available after
/// discovery, repository, download, or cancellation work fails later.
/// </summary>
public sealed record DownloaderExecutionResult(
    BackupJob Job,
    DownloaderTriggerState TriggerState,
    string? FailureCode = null)
{
    public bool TriggerAccepted => TriggerState == DownloaderTriggerState.Accepted;
}
