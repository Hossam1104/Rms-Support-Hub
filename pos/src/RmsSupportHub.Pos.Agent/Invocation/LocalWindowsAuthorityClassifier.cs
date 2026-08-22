using System.Security.Principal;
using RmsSupportHub.Pos.Application.Invocation;

namespace RmsSupportHub.Pos.Agent.Invocation;

public interface ILocalWindowsAuthorityClassifier
{
    bool TryClassify(
        WindowsIdentity identity,
        SecurityIdentifier operatorGroupSid,
        out SecurityIdentifier? callerSid,
        out InvocationAuthorizationLevel authorizationLevel);
}

/// <summary>
/// Classifies only the Windows identity supplied by Named Pipe impersonation. No request payload
/// or caller-provided privilege field participates in this decision.
/// </summary>
public sealed class WindowsLocalAuthorityClassifier : ILocalWindowsAuthorityClassifier
{
    private static readonly SecurityIdentifier LocalSystemSid =
        new(WellKnownSidType.LocalSystemSid, domainSid: null);

    private static readonly SecurityIdentifier BuiltinAdministratorsSid =
        new(WellKnownSidType.BuiltinAdministratorsSid, domainSid: null);

    public bool TryClassify(
        WindowsIdentity identity,
        SecurityIdentifier operatorGroupSid,
        out SecurityIdentifier? callerSid,
        out InvocationAuthorizationLevel authorizationLevel)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(operatorGroupSid);

        callerSid = null;
        authorizationLevel = InvocationAuthorizationLevel.Unauthenticated;
        try
        {
            callerSid = identity.User;
            if (callerSid is null)
            {
                return false;
            }

            var principal = new WindowsPrincipal(identity);
            authorizationLevel = callerSid.Equals(LocalSystemSid)
                || principal.IsInRole(BuiltinAdministratorsSid)
                ? InvocationAuthorizationLevel.LocalAdministrator
                : principal.IsInRole(operatorGroupSid)
                    ? InvocationAuthorizationLevel.LocalOperator
                    : InvocationAuthorizationLevel.Unauthenticated;
            return true;
        }
        catch
        {
            authorizationLevel = InvocationAuthorizationLevel.Unauthenticated;
            return false;
        }
    }
}
