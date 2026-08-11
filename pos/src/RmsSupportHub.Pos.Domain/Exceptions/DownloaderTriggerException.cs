namespace RmsSupportHub.Pos.Domain.Exceptions;

using RmsSupportHub.Pos.Domain.Models;

/// <summary>
/// Stable, sanitized failure boundary for a remote downloader trigger. Infrastructure-specific
/// HTTP exceptions may derive from this type, but Application and Agent code depend only on the
/// stable code.
/// </summary>
public class DownloaderTriggerException(
    string code,
    DownloaderTriggerState triggerState = DownloaderTriggerState.Failed)
    : InvalidOperationException("The backup trigger could not be completed.")
{
    public string Code { get; } = code;

    public DownloaderTriggerState TriggerState { get; } = triggerState;
}
