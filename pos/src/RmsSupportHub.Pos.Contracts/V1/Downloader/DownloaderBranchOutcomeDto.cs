namespace RmsSupportHub.Pos.Contracts.V1.Downloader;

/// <summary>Bounded, sanitized outcome for one requested branch.</summary>
public sealed record DownloaderBranchOutcomeDto(
    string BranchCode,
    DownloaderBranchState State,
    int ProgressPercent,
    string? FailureCode = null,
    string? ArtifactId = null);
