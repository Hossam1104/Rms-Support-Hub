namespace RmsSupportHub.Pos.Domain.Interfaces;

/// <summary>
/// Cancellable delay seam for downloader polling. Production uses the system clock; tests can
/// advance a fake clock without sleeping or depending on wall-clock timing.
/// </summary>
public interface IDownloaderDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}
