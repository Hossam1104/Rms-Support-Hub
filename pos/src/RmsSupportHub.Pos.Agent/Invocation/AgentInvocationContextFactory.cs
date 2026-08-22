using System.Security.Principal;
using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Application.Invocation;

namespace RmsSupportHub.Pos.Agent.Invocation;

public interface IAgentInvocationContextFactory
{
    InvocationContext CreateLegacyLoopback(HttpContext context);

    InvocationContext CreateLocalWpf(
        WindowsIdentity identity,
        SecurityIdentifier operatorGroupSid,
        string correlationId);
}

/// <summary>
/// Converts transport-authenticated identities into the shared application context. No request
/// body or client-provided role is inspected here.
/// </summary>
public sealed class AgentInvocationContextFactory(
    IAdministratorGroupChecker administratorGroupChecker,
    IAgentPrincipalSidResolver principalSidResolver) : IAgentInvocationContextFactory
{
    private static readonly SecurityIdentifier LocalSystemSid =
        new(WellKnownSidType.LocalSystemSid, domainSid: null);

    private static readonly SecurityIdentifier BuiltinAdministratorsSid =
        new(WellKnownSidType.BuiltinAdministratorsSid, domainSid: null);

    public InvocationContext CreateLegacyLoopback(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sid = principalSidResolver.TryGetSid(context.User, out var resolvedSid)
            ? resolvedSid
            : string.Empty;
        var isAdministrator = sid.Length > 0
            && administratorGroupChecker.IsInAdministratorsGroup(context.User);

        return new(
            InvocationSource.LegacyLoopbackHttp,
            sid,
            isAdministrator
                ? InvocationAuthorizationLevel.LocalAdministrator
                : InvocationAuthorizationLevel.Unauthenticated,
            CorrelationIdContext.TryGet(context) ?? string.Empty);
    }

    public InvocationContext CreateLocalWpf(
        WindowsIdentity identity,
        SecurityIdentifier operatorGroupSid,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(operatorGroupSid);

        var callerSid = identity.User;
        var level = InvocationAuthorizationLevel.Unauthenticated;
        if (callerSid is not null)
        {
            try
            {
                var principal = new WindowsPrincipal(identity);
                level = callerSid.Equals(LocalSystemSid)
                    || principal.IsInRole(BuiltinAdministratorsSid)
                    ? InvocationAuthorizationLevel.LocalAdministrator
                    : principal.IsInRole(operatorGroupSid)
                        ? InvocationAuthorizationLevel.LocalOperator
                        : InvocationAuthorizationLevel.Unauthenticated;
            }
            catch (Exception)
            {
                level = InvocationAuthorizationLevel.Unauthenticated;
            }
        }

        return new(
            InvocationSource.LocalWpf,
            callerSid?.Value ?? string.Empty,
            level,
            correlationId);
    }
}
