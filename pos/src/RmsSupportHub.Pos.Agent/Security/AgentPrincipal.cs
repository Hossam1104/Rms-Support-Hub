using System.Security.Claims;
using System.Security.Principal;

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
