using System.Security.Claims;

namespace RmsSupportHub.Pos.Agent.Authorization;

public interface IAdministratorGroupChecker
{
    bool IsInAdministratorsGroup(ClaimsPrincipal principal);
}

/// <summary>
/// Resolves local Built-in Administrators membership from the authenticated Windows identity.
/// The implementation must not use the browser access token's elevation state.
/// </summary>
public interface IWindowsLocalGroupMembershipResolver
{
    bool IsMemberOfBuiltinAdministrators(System.Security.Principal.WindowsIdentity identity);
}

/// <summary>
/// Narrow operating-system seam used by <see cref="IWindowsLocalGroupMembershipResolver"/>.
/// It is separate so membership decisions can be tested without changing the production native
/// Windows API path.
/// </summary>
public interface IWindowsLocalGroupMembershipLookup
{
    bool TryResolveAccountName(
        System.Security.Principal.SecurityIdentifier accountSid,
        out string accountName);

    bool TryGetLocalGroupNames(
        string accountName,
        out IReadOnlyList<string> groupNames);

    bool TryResolveSid(
        string accountName,
        out System.Security.Principal.SecurityIdentifier sid);
}
