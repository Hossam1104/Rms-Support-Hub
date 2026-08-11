using Microsoft.AspNetCore.Http;
using RmsSupportHub.Pos.Agent.Security;

namespace RmsSupportHub.Pos.Agent.MutationTokens;

/// <summary>
/// Future mutation endpoints supply a server-owned operation ID at composition time. The filter
/// reads the opaque token from one header only; it never accepts a query, path, cookie, or body token.
/// </summary>
public sealed class MutationTokenEndpointFilter : IEndpointFilter
{
    private readonly string _operationId;

    public MutationTokenEndpointFilter(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            throw new ArgumentException("A server-owned operation ID is required.", nameof(operationId));
        }

        _operationId = operationId;
    }

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (!AgentPrincipal.TryGetSid(context.HttpContext.User, out var sid))
        {
            return new ValueTask<object?>(Results.Unauthorized());
        }

        var request = context.HttpContext.Request;
        var token = request.Headers[MutationTokenContract.HeaderName].ToString();
        var origin = request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(origin))
        {
            return new ValueTask<object?>(AgentProblemDetails.CreateResult(
                context.HttpContext,
                StatusCodes.Status403Forbidden,
                "The mutation request token is required.",
                AgentProblemCodes.MutationTokenInvalid));
        }

        var store = context.HttpContext.RequestServices.GetRequiredService<IMutationTokenStore>();
        var result = store.TryConsume(new MutationTokenValidationRequest(
            token,
            sid,
            origin,
            request.Method,
            _operationId));

        if (!result.Succeeded)
        {
            return new ValueTask<object?>(AgentProblemDetails.CreateResult(
                context.HttpContext,
                StatusCodes.Status403Forbidden,
                "The mutation request token is invalid.",
                AgentProblemCodes.MutationTokenInvalid));
        }

        return next(context);
    }
}
