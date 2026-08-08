using System.Text.Json;
using RmsSupportHub.Api.Exceptions;

namespace RmsSupportHub.Api.Middleware;

/// <summary>Single choke point turning any exception into the uniform
/// `{ error: { code, message, details } }` envelope (see
/// remediation_plan.md B22). ApiException subclasses carry their own status
/// code; anything else (a raw SqlException, a NullReferenceException, ...)
/// is an unexpected failure and maps to 500 -- previously every exception,
/// including database failures, was flattened to 400.</summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "{Code}: {Message}", ex.Code, ex.Message);
            await WriteEnvelopeAsync(context, ex.StatusCode, ex.Code, ex.Message, ex.Details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            await WriteEnvelopeAsync(context, StatusCodes.Status500InternalServerError, "internal_error", ex.Message, details: null);
        }
    }

    private static Task WriteEnvelopeAsync(HttpContext context, int statusCode, string code, string message, object? details)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new
        {
            error = new { code, message, details }
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return context.Response.WriteAsync(json);
    }
}
