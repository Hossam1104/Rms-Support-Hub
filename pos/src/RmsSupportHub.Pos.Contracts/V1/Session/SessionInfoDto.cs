namespace RmsSupportHub.Pos.Contracts.V1.Session;

/// <summary>
/// Response for <c>GET /api/v1/session</c>: the authenticated Windows principal's safe session
/// diagnostics plus capability/version metadata. The raw Windows SID is not returned.
/// </summary>
public sealed record SessionInfoDto(
    /// <summary>OS-provided display name of the Windows principal authenticated by Negotiate.</summary>
    string PrincipalName,
    /// <summary>
    /// Whether the account is a member of the local Built-in Administrators group. This is resolved
    /// from OS account membership independently of UAC browser-token elevation.
    /// </summary>
    bool IsAuthorized,
    /// <summary>Installed Agent assembly version produced by the Agent.</summary>
    string AgentVersion,
    /// <summary>Agent contract version produced by the Agent.</summary>
    string ApiVersion,
    /// <summary>Contract versions the installed Agent can serve.</summary>
    IReadOnlyList<string> SupportedApiVersions);
