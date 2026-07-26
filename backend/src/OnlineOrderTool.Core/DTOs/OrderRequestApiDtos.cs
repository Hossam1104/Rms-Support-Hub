namespace OnlineOrderTool.Core.DTOs;

/// <summary>Query-string binding for GET /api/modules/{key}/order-requests.
/// q is a convenience alias for OrderNumber (there is no full-text search in
/// OrderRequestRepository -- see R4). Status[] binds repeated
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
    string? Sort
);

/// <summary>EndpointKey is accepted for forward compatibility with the
/// customUrl -> endpointKey -> environment.CancelUrl precedence order the
/// plan specifies, but ModuleEnvironment has only a single CancelUrl today
/// (no multiple named endpoints to select between), so it currently has no
/// effect beyond falling through to CancelUrl.</summary>
public record OrderRequestCancelRequest(string Reason, string? EndpointKey, string? CustomUrl);

public record OrderRequestResendRequest(string BranchCode, string? EndpointKey);
