using Microsoft.AspNetCore.Http;
using RmsSupportHub.Pos.Agent.Security;

namespace RmsSupportHub.Pos.Agent.MutationTokens;

/// <summary>
/// Issues a token only for the authenticated Windows SID, exact configured Origin, and the target
/// method resolved from the server-owned operation registry. The issuance request's POST method
/// is never used as the future mutation target.
/// </summary>
public sealed class MutationTokenService(IMutationTokenStore store, AgentSecurityOptions options)
{
    public MutationTokenIssue Issue(HttpContext context, MutationOperationDescriptor operation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);

        if (!AgentPrincipal.TryGetSid(context.User, out var sid))
        {
            throw new InvalidOperationException("The authenticated Windows SID could not be resolved.");
        }

        var origin = context.Request.Headers.Origin.ToString();
        if (!options.IsAllowedOrigin(origin))
        {
            throw new InvalidOperationException("The request Origin is not the configured Support Hub origin.");
        }

        return store.Issue(sid, origin, operation.HttpMethod, operation.OperationId);
    }
}
