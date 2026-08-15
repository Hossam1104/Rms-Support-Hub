using RmsSupportHub.Pos.Agent.Security;

namespace RmsSupportHub.Pos.Agent.MutationTokens;

/// <summary>
/// Applies the common mutation boundary used by Slice B POST operations. The caller still owns
/// typed request validation and idempotency; this helper only proves that the request is the exact
/// server-registered operation for the authenticated principal and configured Support Hub origin.
/// </summary>
public static class MutationAuthorization
{
    public static MutationAuthorizationResult TryConsume(
        HttpContext context,
        MutationOperationDescriptor operation,
        IMutationTokenStore tokens,
        IAgentPrincipalSidResolver principalSidResolver,
        AgentSecurityOptions securityOptions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(principalSidResolver);
        ArgumentNullException.ThrowIfNull(securityOptions);

        if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
        {
            return new(false, null, MutationAuthorizationFailure.PrincipalUnavailable);
        }

        var token = context.Request.Headers[MutationTokenContract.HeaderName];
        var origin = context.Request.Headers.Origin;
        if (token.Count != 1
            || origin.Count != 1
            || string.IsNullOrWhiteSpace(token[0])
            || !securityOptions.IsAllowedOrigin(origin[0]))
        {
            return new(false, principalSid, MutationAuthorizationFailure.TokenInvalid);
        }

        if (!string.Equals(context.Request.Method, operation.HttpMethod, StringComparison.Ordinal)
            || !operation.TryResolveHttpPath(null, out var expectedPath)
            || !string.Equals(context.Request.Path.Value, expectedPath, StringComparison.Ordinal))
        {
            return new(false, principalSid, MutationAuthorizationFailure.TokenInvalid);
        }

        var consumed = tokens.TryConsume(new MutationTokenValidationRequest(
            token[0]!,
            principalSid,
            origin[0]!,
            context.Request.Method,
            operation.OperationId,
            context.Request.Path.Value));
        return consumed.Succeeded
            ? new(true, principalSid, MutationAuthorizationFailure.None)
            : new(false, principalSid, MutationAuthorizationFailure.TokenInvalid);
    }
}

public enum MutationAuthorizationFailure
{
    None,
    PrincipalUnavailable,
    TokenInvalid
}

public sealed record MutationAuthorizationResult(
    bool Succeeded,
    string? PrincipalSid,
    MutationAuthorizationFailure Failure);
