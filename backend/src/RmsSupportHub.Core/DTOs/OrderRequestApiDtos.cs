namespace RmsSupportHub.Core.DTOs;

/// <summary>Query-string binding for GET /api/modules/{key}/order-requests.
/// q is a convenience alias for OrderNumber. exactMatch defaults to true and
/// controls whether the order-number search uses equality or an escaped
/// contains predicate. Status[] binds repeated
/// `?status=6&amp;status=7` query params (R9 multi-select status chips);
/// Status (singular) is kept for a single-value caller.</summary>
public record OrderRequestListQuery(
    string? Q,
    string? OrderNumber,
    string? Phone,
    string? BranchCode,
    int? Status,
    int[]? Statuses,
    bool? Succeeded,
    bool? HasException,
    DateTime? DateFrom,
    DateTime? DateTo,
    int? Page,
    int? PageSize,
    string? Sort,
    bool? ExactMatch
);

public record OrderRequestCancelRequest(string Reason);

public record OrderRequestResendRequest(string? BranchCode);
