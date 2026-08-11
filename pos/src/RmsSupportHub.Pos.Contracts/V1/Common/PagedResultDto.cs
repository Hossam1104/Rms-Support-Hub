namespace RmsSupportHub.Pos.Contracts.V1.Common;

/// <summary>Generic page envelope for list endpoints (e.g. the activity timeline).</summary>
public sealed record PagedResultDto<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
