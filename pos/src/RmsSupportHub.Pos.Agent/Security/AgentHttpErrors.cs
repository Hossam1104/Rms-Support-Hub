using Microsoft.AspNetCore.Http;

namespace RmsSupportHub.Pos.Agent.Security;

internal static class AgentHttpErrors
{
    public static Task WriteAsync(HttpContext context, int statusCode, string title, string code) =>
        AgentProblemDetails.WriteAsync(context, statusCode, title, code);
}
