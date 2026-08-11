namespace RmsSupportHub.Pos.Contracts.V1.Session;

/// <summary>
/// Response for <c>GET /api/v1/session</c>: current principal plus capability/version metadata
/// (plan section 5.2). The Angular shell uses <see cref="ApiVersion"/> against
/// <see cref="SupportedApiVersions"/> to detect an incompatible agent (plan section 5.1/7.1).
/// </summary>
public sealed record SessionInfoDto(
    string PrincipalName,
    bool IsAuthorized,
    string AgentVersion,
    string ApiVersion,
    IReadOnlyList<string> SupportedApiVersions);
