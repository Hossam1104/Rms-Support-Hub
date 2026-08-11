using RmsSupportHub.Pos.Agent.Correlation;

namespace RmsSupportHub.Pos.Agent.Security;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const int MaxCorrelationIdLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdContext.HeaderName, out var existing)
            && existing.Count == 1
            && IsSafe(existing[0])
            ? existing[0]!
            : Guid.NewGuid().ToString("N");

        context.Items[CorrelationIdContext.HttpContextItemKey] = correlationId;
        context.Response.Headers[CorrelationIdContext.HeaderName] = correlationId;

        await next(context);
    }

    private static bool IsSafe(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaxCorrelationIdLength
        && value.All(character => character is >= '!' and <= '~');
}
