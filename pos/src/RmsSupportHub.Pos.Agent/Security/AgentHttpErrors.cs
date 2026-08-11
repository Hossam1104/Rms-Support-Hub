using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace RmsSupportHub.Pos.Agent.Security;

internal static class AgentHttpErrors
{
    public static Task WriteAsync(HttpContext context, int statusCode, string title)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var body = JsonSerializer.Serialize(new
        {
            type = "about:blank",
            title,
            status = statusCode,
            instance = context.Request.Path.Value
        });
        return context.Response.WriteAsync(body);
    }
}
