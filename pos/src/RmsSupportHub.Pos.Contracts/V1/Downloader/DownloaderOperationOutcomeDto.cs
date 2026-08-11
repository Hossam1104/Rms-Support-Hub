namespace RmsSupportHub.Pos.Contracts.V1.Downloader;

/// <summary>
/// Downloader-specific operation truth. Only logical branch codes, stable codes, and opaque
/// artifact capabilities are exposed.
/// </summary>
public sealed record DownloaderOperationOutcomeDto(
    IReadOnlyList<DownloaderBranchOutcomeDto> Branches,
    string? Serial,
    DownloaderTriggerStateDto TriggerState,
    string? OperatorGuidance = null)
{
    /// <summary>
    /// Compatibility projection for existing consumers. It is never the sole representation of
    /// an unknown trigger outcome; inspect <see cref="TriggerState"/> as well.
    /// </summary>
    public bool TriggerAccepted => TriggerState == DownloaderTriggerStateDto.Accepted;
}
