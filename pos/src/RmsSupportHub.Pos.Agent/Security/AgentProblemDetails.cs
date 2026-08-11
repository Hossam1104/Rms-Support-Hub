using System.Text.Json;
using Microsoft.AspNetCore.Http;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Contracts.V1.Common;

namespace RmsSupportHub.Pos.Agent.Security;

internal static class AgentProblemDetails
{
    public static IResult CreateResult(HttpContext context, int statusCode, string title, string code) =>
        Results.Json(
            CreateDto(context, statusCode, title, code),
            statusCode: statusCode,
            contentType: "application/problem+json");

    public static Task WriteAsync(HttpContext context, int statusCode, string title, string code)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(CreateDto(context, statusCode, title, code)));
    }

    private static AgentProblemDetailsDto CreateDto(
        HttpContext context,
        int statusCode,
        string title,
        string code) =>
        new(
            "about:blank",
            title,
            statusCode,
            code,
            CorrelationIdContext.TryGet(context));
}
