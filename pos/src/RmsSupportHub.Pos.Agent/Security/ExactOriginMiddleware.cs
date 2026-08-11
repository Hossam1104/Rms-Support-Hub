using Microsoft.AspNetCore.Http;

namespace RmsSupportHub.Pos.Agent.Security;

/// <summary>
/// CORS is a transport permission, not application authorization. This independent gate rejects
/// missing or unknown browser origins for Agent API requests after authentication/authorization has
/// had a chance to handle anonymous preflight.
/// </summary>
public sealed class ExactOriginMiddleware(RequestDelegate next, AgentSecurityOptions options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsBrowserApiRequest(context.Request) && !IsPreflight(context.Request))
        {
            var origins = context.Request.Headers.Origin;
            if (origins.Count != 1 || !options.IsAllowedOrigin(origins[0]))
            {
                await AgentHttpErrors.WriteAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "The request origin is not accepted.",
                    AgentProblemCodes.OriginRejected);
                return;
            }
        }

        await next(context);
    }

    private static bool IsBrowserApiRequest(HttpRequest request) =>
        request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase);

    private static bool IsPreflight(HttpRequest request) =>
        HttpMethods.IsOptions(request.Method)
        && request.Headers.ContainsKey("Origin")
        && request.Headers.ContainsKey("Access-Control-Request-Method");
}
