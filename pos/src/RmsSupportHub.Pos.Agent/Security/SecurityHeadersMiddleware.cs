namespace RmsSupportHub.Pos.Agent.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ApiContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    private const string ScalarContentSecurityPolicy =
        "default-src 'none'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; connect-src 'self'; font-src 'self'; worker-src 'self' blob:; " +
        "object-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Content-Security-Policy"] =
            IsScalarDocument(context.Request.Path)
                ? ScalarContentSecurityPolicy
                : ApiContentSecurityPolicy;
        context.Response.OnStarting(() =>
        {
            var hasOriginVariation = context.Response.Headers.TryGetValue("Vary", out var varyValues)
                && varyValues.ToString().Split(',', StringSplitOptions.TrimEntries)
                    .Any(item => string.Equals(item, "Origin", StringComparison.OrdinalIgnoreCase));
            if (context.Request.Headers.ContainsKey("Origin") && !hasOriginVariation)
            {
                context.Response.Headers.Append("Vary", "Origin");
            }

            return Task.CompletedTask;
        });

        await next(context);
    }

    private static bool IsScalarDocument(PathString path)
    {
        return path.Equals("/scalar") || path.StartsWithSegments("/scalar/");
    }
}
