using Microsoft.AspNetCore.Http;

namespace RmsSupportHub.Pos.Agent.Security;

/// <summary>
/// Performs the Agent-owned fail-closed transport check before ASP.NET Core's CORS middleware. The
/// CORS middleware otherwise returns an empty 204 for some invalid preflights, which is not an
/// explicit rejection contract for the loopback boundary.
/// </summary>
public sealed class CorsPreflightValidationMiddleware(RequestDelegate next, AgentSecurityOptions options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsApiPreflight(context.Request))
        {
            var origin = context.Request.Headers.Origin;
            var requestedMethod = context.Request.Headers["Access-Control-Request-Method"].ToString();
            var requestedHeaders = context.Request.Headers["Access-Control-Request-Headers"].ToString();

            if (origin.Count != 1
                || !options.IsAllowedOrigin(origin[0])
                || !AgentCors.AllowedMethods.Contains(requestedMethod, StringComparer.OrdinalIgnoreCase)
                || !RequestedHeadersAreAllowed(requestedHeaders))
            {
                await AgentHttpErrors.WriteAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "The CORS preflight request is not accepted.");
                return;
            }
        }

        await next(context);
    }

    private static bool IsApiPreflight(HttpRequest request) =>
        HttpMethods.IsOptions(request.Method)
        && request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase)
        && request.Headers.ContainsKey("Origin")
        && request.Headers.ContainsKey("Access-Control-Request-Method");

    private static bool RequestedHeadersAreAllowed(string requestedHeaders)
    {
        if (string.IsNullOrWhiteSpace(requestedHeaders))
        {
            return true;
        }

        return requestedHeaders
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(requested => AgentCors.AllowedHeaders.Contains(requested, StringComparer.OrdinalIgnoreCase));
    }
}
