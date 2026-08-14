using RmsSupportHub.Pos.Contracts.V1.Common;

namespace RmsSupportHub.Pos.Contracts.V1.Services;

/// <summary>
/// One row of <c>GET /api/v1/services</c>. <see cref="ServiceId"/> is a server-issued opaque ID —
/// the raw Windows service name is never accepted from a request (plan section 5.2, section 7).
/// </summary>
public sealed record ServiceSummaryDto(
    string ServiceId,
    string DisplayName,
    bool Installed,
    ServiceRuntimeState State,
    EvidenceDto LastChecked,
    IReadOnlyList<ServiceActionKind> AllowedActions,
    string? LastOutcome);
