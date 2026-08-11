namespace RmsSupportHub.Pos.Agent.Correlation;

public static class CorrelationIdContext
{
    public const string HeaderName = "X-Correlation-Id";
    public const string HttpContextItemKey = "CorrelationId";

    public static string? TryGet(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(HttpContextItemKey, out var value) ? value as string : null;
}
