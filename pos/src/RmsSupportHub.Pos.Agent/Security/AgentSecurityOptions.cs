namespace RmsSupportHub.Pos.Agent.Security;

/// <summary>
/// Environment-specific browser trust input. The Agent origin itself is fixed in
/// <see cref="AgentHostConstants"/>; only the one exact Support Hub origin and the deployment-owned
/// LocalMachine certificate selector are supplied by deployment configuration.
/// </summary>
public sealed class AgentSecurityOptions
{
    public const string SectionName = "PosAgentSecurity";

    public string? SupportHubOrigin { get; set; }

    public string? CertificateThumbprint { get; set; }

    public void Validate()
    {
        if (!TryNormalizeOrigin(SupportHubOrigin, out var normalizedOrigin))
        {
            throw new InvalidOperationException(
                $"{SectionName}:SupportHubOrigin must be one absolute HTTPS origin without a path, query, fragment, or wildcard.");
        }

        SupportHubOrigin = normalizedOrigin;
    }

    public bool IsAllowedOrigin(string? origin) =>
        TryNormalizeOrigin(origin, out var normalizedOrigin)
        && string.Equals(SupportHubOrigin, normalizedOrigin, StringComparison.OrdinalIgnoreCase);

    public static bool TryNormalizeOrigin(string? value, out string normalizedOrigin)
    {
        normalizedOrigin = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains('*', StringComparison.Ordinal)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !uri.IsAbsoluteUri
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || uri.AbsolutePath is not "" and not "/")
        {
            return false;
        }

        var authority = uri.GetLeftPart(UriPartial.Authority);
        if (authority.Length == 0 || authority.Contains('*', StringComparison.Ordinal))
        {
            return false;
        }

        normalizedOrigin = authority;
        return true;
    }

    public static bool IsSingleExactOrigin(IList<string> origins, string? expectedOrigin)
    {
        return origins.Count == 1
            && TryNormalizeOrigin(origins[0], out var normalized)
            && string.Equals(normalized, expectedOrigin, StringComparison.OrdinalIgnoreCase);
    }
}
