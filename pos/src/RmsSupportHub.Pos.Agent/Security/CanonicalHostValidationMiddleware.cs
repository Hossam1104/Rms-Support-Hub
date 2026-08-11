using Microsoft.AspNetCore.Http;

namespace RmsSupportHub.Pos.Agent.Security;

public sealed class CanonicalHostValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsCanonicalHost(context.Request.Host))
        {
            await AgentHttpErrors.WriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                "The request host is not accepted.",
                AgentProblemCodes.HostRejected);
            return;
        }

        await next(context);
    }

    public static bool IsCanonicalHost(HostString host) =>
        string.Equals(host.Host, AgentHostConstants.CanonicalHost, StringComparison.OrdinalIgnoreCase)
        && host.Port == AgentHostConstants.Port;
}
