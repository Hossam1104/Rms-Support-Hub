using System.Security.Claims;
using System.Security.Principal;

namespace RmsSupportHub.Pos.Agent.Authorization;

/// <summary>
/// Uses the Windows access token produced by Negotiate. Role-shaped client claims are not accepted
/// as a production substitute for the operating-system group check.
/// </summary>
public sealed class WindowsAdministratorGroupChecker : IAdministratorGroupChecker
{
    public bool IsInAdministratorsGroup(ClaimsPrincipal principal)
    {
        return principal.Identity is WindowsIdentity windowsIdentity
            && new WindowsPrincipal(windowsIdentity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
