namespace OnlineOrderTool.Core.DTOs;

/// <summary>Never carries the real ApiUrl/CancelUrl -- internal RMS endpoint
/// topology should not be published to the browser (see
/// remediation_plan.md B16). HasApiUrl/HasCancelUrl are enough for the UI to
/// know whether an environment can send/cancel.</summary>
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
    bool HasApiUrl,
    bool HasCancelUrl
);

/// <summary>Mirrors OnlineOrderTool.Core.Modules.ModuleCapabilities -- exposed
/// on ModuleDto so the frontend can gate routes/UI on real capability data
/// (e.g. a route guard for Capabilities.OrderRequests) instead of hardcoding
/// module-key checks that would drift from the backend (see
/// remediation_plan.md B21, R7).</summary>
public record ModuleCapabilitiesDto(
    string? DraftKind,
    bool ItemLookup,
    bool ConsumerLookup,
    bool OrderRequests,
    bool Cancel,
    bool Resend,
    bool HasDeliveryFields
);

public record ModuleDto(
    string Key,
    string Label,
    string Client,
    bool Available,
    List<EnvironmentDto> Environments,
    ModuleCapabilitiesDto Capabilities
);

public record UpdateOrderFieldRequest(string FieldName, object? Value);

public record SendOrderRequest(string? EnvironmentKey, string? CustomApiUrl);

public record CancelOrderRequest(string OrderNumber, string CancelReason);

public record LookupResultDto(
    bool Success,
    string? Message,
    object? Data
);
