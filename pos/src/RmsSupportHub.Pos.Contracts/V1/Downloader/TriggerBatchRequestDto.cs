namespace RmsSupportHub.Pos.Contracts.V1.Downloader;

/// <summary>
/// <c>POST /api/v1/downloads/batches</c> request. Never carries an SMB path or RDB credential —
/// those are resolved server-side from stored, encrypted configuration (plan section 12).
/// </summary>
public sealed record TriggerBatchRequestDto(IReadOnlyList<string> BranchCodes, string IdempotencyKey);
