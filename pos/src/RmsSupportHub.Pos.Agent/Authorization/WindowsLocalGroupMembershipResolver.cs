using System.Security.Principal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RmsSupportHub.Pos.Agent.Correlation;

namespace RmsSupportHub.Pos.Agent.Authorization;

/// <summary>
/// Determines local Administrator membership from the authenticated account rather than from the
/// current browser token. NetUserGetLocalGroups is configured by the lookup adapter to include
/// indirect membership, and the returned localized group names are compared by SID.
/// </summary>
public sealed class WindowsLocalGroupMembershipResolver(
    IWindowsLocalGroupMembershipLookup lookup,
    IHttpContextAccessor httpContextAccessor,
    ILogger<WindowsLocalGroupMembershipResolver> logger) : IWindowsLocalGroupMembershipResolver
{
    private static readonly SecurityIdentifier BuiltinAdministratorsSid =
        new(WellKnownSidType.BuiltinAdministratorsSid, domainSid: null);

    public bool IsMemberOfBuiltinAdministrators(WindowsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        try
        {
            if (identity.User is not { } accountSid
                || !lookup.TryResolveAccountName(accountSid, out var accountName)
                || !lookup.TryGetLocalGroupNames(accountName, out var groupNames)
                || groupNames.Count == 0)
            {
                return FailClosed();
            }

            foreach (var groupName in groupNames)
            {
                if (!lookup.TryResolveSid(groupName, out var groupSid))
                {
                    return FailClosed();
                }

                if (BuiltinAdministratorsSid.Equals(groupSid))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception)
        {
            return FailClosed();
        }
    }

    private bool FailClosed()
    {
        var correlationId = httpContextAccessor.HttpContext is { } context
            ? CorrelationIdContext.TryGet(context)
            : null;

        logger.LogWarning(
            "Windows local-group membership resolution failed. CorrelationId: {CorrelationId}",
            correlationId ?? "unavailable");
        return false;
    }
}
