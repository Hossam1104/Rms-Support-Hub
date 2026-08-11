using Microsoft.AspNetCore.Http;
using RmsSupportHub.Pos.Agent.Security;

namespace RmsSupportHub.Pos.Agent.MutationTokens;

/// <summary>
/// Issues a token only for the authenticated Windows SID, exact configured Origin, current HTTP
/// method, and a server-owned operation identifier. INT-04 intentionally exposes no issuance route.
/// </summary>
public sealed class MutationTokenService(IMutationTokenStore store, AgentSecurityOptions options)
{
    public MutationTokenIssue Issue(HttpContext context, string operationId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

        if (!AgentPrincipal.TryGetSid(context.User, out var sid))
        {
            throw new InvalidOperationException("The authenticated Windows SID could not be resolved.");
        }

        var origin = context.Request.Headers.Origin.ToString();
        if (!options.IsAllowedOrigin(origin))
        {
            throw new InvalidOperationException("The request Origin is not the configured Support Hub origin.");
        }

        return store.Issue(sid, origin, context.Request.Method, operationId);
    }
}
