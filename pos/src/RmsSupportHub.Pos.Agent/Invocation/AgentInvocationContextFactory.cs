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
    IAgentPrincipalSidResolver principalSidResolver,
    ILocalWindowsAuthorityClassifier localWindowsAuthorityClassifier) : IAgentInvocationContextFactory
{
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

        SecurityIdentifier? callerSid = null;
        var level = InvocationAuthorizationLevel.Unauthenticated;
        try
        {
            if (!localWindowsAuthorityClassifier.TryClassify(
                    identity,
                    operatorGroupSid,
                    out callerSid,
                    out level))
            {
                level = InvocationAuthorizationLevel.Unauthenticated;
            }
        }
        catch (Exception)
        {
            callerSid = null;
            level = InvocationAuthorizationLevel.Unauthenticated;
        }

        return new(
            InvocationSource.LocalWpf,
            callerSid?.Value ?? string.Empty,
            level,
            correlationId);
    }
}
