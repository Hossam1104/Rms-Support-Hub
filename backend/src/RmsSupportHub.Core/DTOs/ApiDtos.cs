namespace RmsSupportHub.Core.DTOs;

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
    bool HasCancelUrl,
    bool IsDefault
);

/// <summary>Mirrors RmsSupportHub.Core.Modules.ModuleCapabilities -- exposed
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
    bool HasDeliveryFields,
    bool BranchLookup
);

public record ModuleDto(
    string Key,
    string Label,
    string Client,
    bool Available,
    List<EnvironmentDto> Environments,
    ModuleCapabilitiesDto Capabilities
);

/// <summary>Per-environment reachability for the dashboard. Carries the module
/// and environment keys the browser already knows plus a status name -- never
/// the probed host, port, or URL, so the module catalog keeps the endpoint
/// topology private (remediation_plan.md B16).</summary>
public record EnvironmentHealthDto(
    string ModuleKey,
    string EnvironmentKey,
    string Status,
    DateTimeOffset CheckedAt
);

/// <summary>U2 (UI_Rework_Plan.md D1): applies every field in Fields inside
/// one synchronised DraftManager.PatchOrderDataAsync load-modify-write
/// instead of one HTTP round trip per field.</summary>
public record PatchOrderDataRequest(Dictionary<string, object?> Fields);

/// <summary>U4 (UI_Rework_Plan.md D13): the active environment's resolved
/// send endpoint, so the operator can see where Send will post before
/// sending. Deliberately scoped and additive: the module catalog
/// (GET /api/modules) still never carries URLs (remediation_plan.md B16) --
/// this returns only the single resolved environment's ApiUrl, the same URL
/// every send response already discloses as urlSent. It never carries
/// CancelUrl, connection strings, or credentials.</summary>
public record ModuleEndpointDto(string EnvironmentKey, string Environment, string? ApiUrl);

public record SendOrderRequest(string? EnvironmentKey);

public record CancelOrderRequest(string OrderNumber, string CancelReason, string? EnvironmentKey);

public record ProductionUnlockRequest(string Password);

public record ProductionUnlockResponse(string Token, DateTimeOffset ExpiresAt);

public record LookupResultDto(
    bool Success,
    string? Message,
    object? Data
);
