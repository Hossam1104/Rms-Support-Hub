namespace RmsSupportHub.Pos.Contracts.V1.Downloader;

/// <summary>
/// Principal-scoped downloader progress and outcome. The operation ID and artifact IDs are opaque;
/// storage paths, SMB details, credentials, and exception text never cross this contract.
/// </summary>
public sealed record DownloaderOperationDto(
    string OperationId,
    DownloaderOperationStateDto State,
    DownloaderOperationStateDto Outcome,
    int ProgressPercent,
    string Stage,
    string Detail,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DownloaderOperationOutcomeDto? DownloaderOutcome,
    string? ErrorCode,
    string CorrelationId);
