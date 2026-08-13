using System.Security.Claims;
using System.Security.Principal;

namespace RmsSupportHub.Pos.Agent.Authorization;

/// <summary>
/// Checks membership of the local Built-in Administrators group through the operating-system
/// resolver. It deliberately does not inspect the browser token's elevation state.
/// </summary>
public sealed class WindowsAdministratorGroupChecker(
    IWindowsLocalGroupMembershipResolver membershipResolver) : IAdministratorGroupChecker
{
    public bool IsInAdministratorsGroup(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity is not WindowsIdentity windowsIdentity
            || windowsIdentity.User is null)
        {
            return false;
        }

        try
        {
            return membershipResolver.IsMemberOfBuiltinAdministrators(windowsIdentity);
        }
        catch (Exception)
        {
            // An unexpected resolver failure is an authorization failure, never an allow.
            return false;
        }
    }
}
