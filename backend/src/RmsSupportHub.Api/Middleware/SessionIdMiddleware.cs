namespace RmsSupportHub.Api.Middleware;

/// <summary>Issues a per-browser session id on first contact (see
/// remediation_plan.md B19 -- DraftManager used to be a process-global
/// singleton file, so two browsers shared and overwrote one draft) and makes
/// it available to controllers via HttpContext.GetSessionId(). The id is a
/// bare Guid ("N" format: no dashes) so it is always safe to use as a
/// directory name in DraftManager without further sanitization.</summary>
public class SessionIdMiddleware
{
    public const string CookieName = "oot_sid";

    private readonly RequestDelegate _next;

    public SessionIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sessionId = context.Request.Cookies[CookieName];

        if (string.IsNullOrEmpty(sessionId) || !Guid.TryParseExact(sessionId, "N", out _))
        {
            sessionId = Guid.NewGuid().ToString("N");
            context.Response.Cookies.Append(CookieName, sessionId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
        }

        context.Items[CookieName] = sessionId;
        await _next(context);
    }
}

public static class HttpContextSessionExtensions
{
    /// <summary>Never throws in practice -- SessionIdMiddleware is
    /// registered unconditionally in Program.cs ahead of MapControllers, so
    /// every request that reaches a controller already has one.</summary>
    public static string GetSessionId(this HttpContext context) =>
        context.Items[SessionIdMiddleware.CookieName] as string
            ?? throw new InvalidOperationException(
                "No session id on HttpContext.Items -- is SessionIdMiddleware registered ahead of MapControllers in Program.cs?");
}
