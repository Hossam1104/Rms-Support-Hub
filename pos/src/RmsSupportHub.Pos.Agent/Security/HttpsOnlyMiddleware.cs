using Microsoft.AspNetCore.Http;

namespace RmsSupportHub.Pos.Agent.Security;

public sealed class HttpsOnlyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.IsHttps)
        {
            await AgentHttpErrors.WriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                "The Agent accepts HTTPS requests only.");
            return;
        }

        await next(context);
    }
}
