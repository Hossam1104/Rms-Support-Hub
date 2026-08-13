using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Security;
using RmsSupportHub.Pos.Contracts.V1.Session;

namespace RmsSupportHub.Pos.Agent;

/// <summary>
/// Adds concise, security-aware descriptions to schemas and properties reachable from the current
/// Agent foundation operations.
/// </summary>
public sealed class AgentOpenApiSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var type = context.JsonPropertyInfo?.DeclaringType ?? context.JsonTypeInfo.Type;
        var propertyName = context.JsonPropertyInfo?.Name;

        schema.Description = (type, propertyName) switch
        {
            (var value, null) when value == typeof(HealthStatusDto) =>
                "Anonymous health response produced by the Agent. Status identifies the foundation " +
                "check that answered; it contains no machine or credential detail.",
            (var value, null) when value == typeof(SessionInfoDto) =>
                "Security and API-version diagnostics produced by the Agent for the authenticated " +
                "Windows account. The raw Windows SID is intentionally omitted.",
            (var value, null) when value == typeof(MutationTokenIssueRequestDto) =>
                "Browser request for a token for one logical operation known to the server. The " +
                "browser cannot supply the target HTTP method or path.",
            (var value, null) when value == typeof(MutationTokenIssueResponseDto) =>
                "Opaque, short-lived mutation-token response produced by the Agent. The token is " +
                "intended for browser memory only.",
            (var value, null) when value == typeof(AgentProblemDetailsDto) =>
                "Safe application/problem+json error contract. Code is a stable problem code and " +
                "correlationId may identify the request; sensitive identity and machine details are " +
                "not included.",
            _ => schema.Description
        };

        var propertyDescription = GetPropertyDescription(type, propertyName);
        if (propertyDescription is not null)
        {
            schema.Description = propertyDescription;
        }

        return Task.CompletedTask;
    }

    private static string? GetPropertyDescription(Type type, string? propertyName) =>
        (type, propertyName) switch
        {
            (var value, "status") when value == typeof(HealthStatusDto) =>
                "Identifies which anonymous foundation health check produced the response (live or ready).",
            (var value, "principalName") when value == typeof(SessionInfoDto) =>
                "The OS-provided display name of the authenticated Windows principal. It is produced " +
                "by Negotiate and is not browser-supplied.",
            (var value, "isAuthorized") when value == typeof(SessionInfoDto) =>
                "Whether the authenticated account is a member of the local Built-in Administrators " +
                "group. This is account membership resolved by Windows and is independent of UAC token " +
                "elevation; the browser cannot supply it.",
            (var value, "agentVersion") when value == typeof(SessionInfoDto) =>
                "The installed Agent assembly version produced by the Agent.",
            (var value, "apiVersion") when value == typeof(SessionInfoDto) =>
                "The Agent contract version produced by the Agent.",
            (var value, "supportedApiVersions") when value == typeof(SessionInfoDto) =>
                "Contract versions the installed Agent can serve, produced by the Agent rather than " +
                "accepted from the browser.",
            (var value, "operationId") when value == typeof(MutationTokenIssueRequestDto) =>
                "Stable logical identifier for a server-registered operation. The browser supplies this " +
                "identifier only; it cannot choose the target path or HTTP method.",
            (var value, "token") when value == typeof(MutationTokenIssueResponseDto) =>
                "Opaque short-lived, one-use token produced by the Agent. It is bound to the authenticated " +
                "Windows SID, exact Origin, operation, and server-resolved method and should remain in " +
                "browser memory.",
            (var value, "expiresAtUtc") when value == typeof(MutationTokenIssueResponseDto) =>
                "UTC expiry instant enforced by the Agent for the one-use mutation token.",
            (var value, "type") when value == typeof(AgentProblemDetailsDto) =>
                "Problem type identifier for the stable Agent error contract.",
            (var value, "title") when value == typeof(AgentProblemDetailsDto) =>
                "Safe human-readable explanation of why the Agent rejected the request.",
            (var value, "status") when value == typeof(AgentProblemDetailsDto) =>
                "HTTP status code returned by the Agent.",
            (var value, "code") when value == typeof(AgentProblemDetailsDto) =>
                "Optional stable machine-readable Agent problem code used by clients; it never carries " +
                "raw identity or credential data.",
            (var value, "correlationId") when value == typeof(AgentProblemDetailsDto) =>
                "Optional request correlation identifier echoed by the Agent for diagnostics.",
            _ => null
        };
}
