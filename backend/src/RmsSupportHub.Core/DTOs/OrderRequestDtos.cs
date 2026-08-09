namespace RmsSupportHub.Core.DTOs;

/// <summary>Normalized filters for OrderRequestRepository.ListAsync/CountAsync/StatsAsync.
/// All optional -- an empty filter set produces no predicates; the default
/// dashboard repository path limits the unfiltered view to the latest ten
/// requests. Statuses (R9, multi-select status chips) takes precedence over the
/// single-value Status when both are supplied; Status is kept for any
/// other/older caller. Phone is normalized to its last nine digits by the API
/// before this model reaches the repository. ExactOrderNumber defaults to true
/// to preserve the original exact-search behavior; false opts into an escaped
/// contains search.</summary>
public record OrderRequestFilters(
    string? OrderNumber = null,
    string? Phone = null,
    string? BranchCode = null,
    int? Status = null,
    IReadOnlyList<int>? Statuses = null,
    bool? Succeeded = null,
    bool? HasException = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    bool ExactOrderNumber = true
);

/// <summary>One row in the Order Requests list. Deliberately excludes
/// RequestJson/ResponseJson -- see RequestBytes/HasResponse instead. Only
/// GetDetailAsync reads the blobs.</summary>
public record OrderRequestListItemDto(
    long Id,
    string OrderNumber,
    DateTime OrderDate,
    decimal NetTotal,
    int ItemCount,
    bool? IsSucceeded,
    long RequestBytes,
    bool HasResponse,
    long? OrderHeaderId,
    string? BranchCode,
    string? BranchName,
    int? OrderStatus,
    string? OrderStatusLabel,
    string? ParentOrderNumber,
    string? InvoiceBarcode,
    DateTime? InvoiceDate
);

public record OrderRequestStatsDto(
    int Total,
    int Succeeded,
    int Failed,
    int Cancelled
);

public record OrderRequestHeaderDto(
    long OrderHeaderId,
    string OrderNumber,
    string BranchCode,
    string? BranchName,
    int OrderStatus,
    string OrderStatusLabel,
    DateTime OrderDate,
    string? ConsumerMobile,
    string? Address,
    decimal GrossTotal,
    decimal NetTotal,
    decimal TotalVat,
    decimal TotalDiscount,
    string? OrderPaymentMethod,
    string? OrderNote,
    string? ParentOrderNumber,
    /// <summary>RequestOrderHeaders.RejectionMessage -- verified real column
    /// (docs/database-schema.md §"RequestOrderHeaders"), added in R9 for the
    /// Overview tab's amber callout; not selected before this session.</summary>
    string? RejectionMessage,
    bool CanResend,
    bool CanCancel
);

public record OrderRequestDetailLineDto(
    string? ItemName,
    string? MaterialNumber,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    decimal TotalDiscount,
    decimal ItemVat,
    decimal ItemVatPercentage,
    string? OfferCode,
    string? OfferMessage
);

public record OrderRequestTransactionDto(
    decimal PaymentAmount,
    string? ECommercePaymentMethod,
    string? ECommercePaymentOption,
    decimal OptionCommission,
    string? PaymentStatus,
    string? TransactionCode,
    string? BankCode,
    string? CardName
);

public record OrderRequestInvoiceDto(
    string? Barcode,
    DateTime? CloseDateLocalTime,
    decimal? NetAmount,
    decimal? PaidAmount
);

/// <summary>Full single-request detail. The only shape that carries
/// RequestJson/ResponseJson/ExceptionMessage -- the two columns
/// (ResponseJson, ExceptionMessage) this whole feature exists to surface.
/// Raw strings only; parsing with a parse-error fallback is a
/// presentation-layer concern for a later session, not the repository's.</summary>
public record OrderRequestDetailDto(
    long Id,
    string OrderNumber,
    DateTime OrderDate,
    decimal NetTotal,
    int ItemCount,
    bool? IsSucceeded,
    string? ExceptionMessage,
    string? RequestJson,
    string? ResponseJson,
    OrderRequestHeaderDto? Header,
    List<OrderRequestDetailLineDto> Details,
    List<OrderRequestTransactionDto> Transactions,
    OrderRequestInvoiceDto? Invoice
);

/// <summary>One row in an order's retry history (every OrderRequests row for
/// the same OrderNumber -- a resend keeps the order number and adds a new
/// attempt row, it does not create a new order number).</summary>
public record OrderRequestAttemptDto(
    long Id,
    DateTime OrderDate,
    bool? IsSucceeded,
    bool HasException
);

public record OrderRequestLineageNodeDto(
    string OrderNumber,
    DateTime OrderDate,
    int? OrderStatus,
    string? OrderStatusLabel,
    decimal? NetTotal
);

public record OrderRequestLineageDto(
    OrderRequestLineageNodeDto? Parent,
    List<OrderRequestLineageNodeDto> Children
);
