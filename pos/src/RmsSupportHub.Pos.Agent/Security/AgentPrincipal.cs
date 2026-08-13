using System.Security.Claims;
using System.Security.Principal;
using RmsSupportHub.Pos.Agent;

namespace RmsSupportHub.Pos.Agent.Security;

/// <summary>
/// Resolves the Agent ownership key from the authenticated server-side principal. Display names,
/// Hub session IDs, and client-supplied owner fields never participate in this resolution.
/// </summary>
public static class AgentPrincipal
{
    public static bool TryGetSid(ClaimsPrincipal principal, out string sid)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity is WindowsIdentity windowsIdentity && windowsIdentity.User is { } windowsSid)
        {
            sid = windowsSid.Value;
            return true;
        }

        sid = string.Empty;
        return false;
    }

    /// <summary>
    /// Resolves the synthetic SID claim used only by the IntegrationTest authentication substitute.
    /// Production callers must use <see cref="TryGetSid"/> so browser-provided claims cannot become
    /// an ownership or authorization source.
    /// </summary>
    public static bool TryGetIntegrationTestSid(ClaimsPrincipal principal, out string sid)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var sidClaim = principal.FindFirst(ClaimTypes.PrimarySid)
            ?? principal.FindFirst(ClaimTypes.Sid)
            ?? principal.FindFirst("sid");

        return TryCanonicalizeSid(sidClaim?.Value, out sid);
    }

    public static bool TryCanonicalizeSid(string? value, out string sid)
    {
        sid = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            sid = new SecurityIdentifier(value).Value;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (IdentityNotMappedException)
        {
            return false;
        }
    }
}

public interface IAgentPrincipalSidResolver
{
    bool TryGetSid(ClaimsPrincipal principal, out string sid);
}

/// <summary>
/// Uses the Windows identity in production. Synthetic SID claims are accepted only by the
/// dedicated IntegrationTest host, which cannot perform a live Negotiate handshake in TestServer.
/// </summary>
public sealed class AgentPrincipalSidResolver(IHostEnvironment environment) : IAgentPrincipalSidResolver
{
    public bool TryGetSid(ClaimsPrincipal principal, out string sid)
    {
        if (AgentPrincipal.TryGetSid(principal, out sid))
        {
            return true;
        }

        if (environment.IsEnvironment(AgentHostConstants.IntegrationTestEnvironment))
        {
            return AgentPrincipal.TryGetIntegrationTestSid(principal, out sid);
        }

        sid = string.Empty;
        return false;
    }
}
