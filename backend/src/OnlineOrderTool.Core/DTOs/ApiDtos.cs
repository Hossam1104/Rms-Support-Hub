namespace OnlineOrderTool.Core.DTOs;

public record EnvironmentDto(
    string Key,
    string Environment,
    string Description,
    string Accent,
    string Cue,
    string Icon,
    string RouteLabel,
    string VisualUrl,
    string VisualAlt,
    bool Available,
    string StatusLabel,
    string? ApiUrl
);

public record ModuleDto(
    string Key,
    string Label,
    string Client,
    bool Available,
    List<EnvironmentDto> Environments
);

public record UpdateOrderFieldRequest(string FieldName, object? Value);

public record SendOrderRequest(string? EnvironmentKey, string? CustomApiUrl);

public record CancelOrderRequest(string OrderNumber, string CancelReason);

public record LookupResultDto(
    bool Success,
    string? Message,
    object? Data
);

public record OrderSearchRequest(
    string? OrderNumber,
    string? Phone,
    string? BranchCode,
    int? Status,
    DateTime? DateFrom,
    DateTime? DateTo
);

public record OrderSearchResultDto(
    long HeaderId,
    string OrderNumber,
    string BranchCode,
    int Status,
    string StatusLabel,
    DateTime CreationDate,
    string? InvoiceBarcode,
    DateTime? InvoiceDate,
    bool CanResend
);
