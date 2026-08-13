using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace RmsSupportHub.Pos.Agent;

/// <summary>
/// Supplies semantic operation and response descriptions for the current foundation API. Endpoint
/// metadata remains the first source for operation names/tags; this transformer fills the details
/// required by the permanent Agent documentation standard.
/// </summary>
public sealed class AgentOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var path = "/" + (context.Description.RelativePath?.TrimStart('/') ?? string.Empty);
        var method = context.Description.HttpMethod?.ToUpperInvariant();

        switch ((method, path))
        {
            case ("GET", "/health/live"):
                DocumentHealthLive(operation);
                break;
            case ("GET", "/health/ready"):
                DocumentHealthReady(operation);
                break;
            case ("GET", "/api/v1/session"):
                DocumentSession(operation);
                break;
            case ("POST", "/api/v1/security/mutation-token"):
                DocumentMutationToken(operation);
                break;
        }

        return Task.CompletedTask;
    }

    private static void DocumentHealthLive(OpenApiOperation operation)
    {
        SetOperation(
            operation,
            "Check Agent liveness",
            "Confirms that the local Agent process is alive and able to answer HTTP requests. " +
            "Authentication and authorization are anonymous/none. The check has no side effects " +
            "and a successful 200 response does not prove POS SQL connectivity, SCM connectivity, " +
            "SMB connectivity, backup readiness, restore readiness, browser authentication, or " +
            "mutation authorization.");
        SetResponseDescription(operation, "200", "The Agent process is alive and returned a HealthStatusDto.");
        SetResponseExample(operation, "200", "application/json", new JsonObject
        {
            ["status"] = "live"
        });
    }

    private static void DocumentHealthReady(OpenApiOperation operation)
    {
        SetOperation(
            operation,
            "Check Agent readiness",
            "Returns the foundation-stage readiness response. Authentication and authorization are " +
            "anonymous/none, and the check has no side effects. At this stage readiness uses the " +
            "same implementation behavior as liveness and does not probe POS SQL, SCM, SMB, backup, " +
            "restore, or feature dependencies.");
        SetResponseDescription(operation, "200", "The Agent returned its current HealthStatusDto readiness state.");
        SetResponseExample(operation, "200", "application/json", new JsonObject
        {
            ["status"] = "ready"
        });
    }

    private static void DocumentSession(OpenApiOperation operation)
    {
        SetOperation(
            operation,
            "Read authenticated Agent session diagnostics",
            "Returns security and API-version diagnostics for the Windows account authenticated by " +
            "Negotiate. Any authenticated Windows account with a resolvable Windows SID may call this " +
            "endpoint. IsAuthorized represents membership in the local machine's Built-in " +
            "Administrators group as resolved by Windows account membership, independently of UAC " +
            "browser-token elevation. The raw Windows SID is not returned to the browser. The endpoint " +
            "has no side effects.");
        SetResponseDescription(operation, "200", "The authenticated account's SessionInfoDto diagnostics and API-version metadata.");
        SetResponseDescription(
            operation,
            "401",
            "The Windows authentication middleware issued a Negotiate challenge. This framework " +
            "response is not guaranteed to contain an AgentProblemDetailsDto body; clients should " +
            "inspect the WWW-Authenticate header.");
        SetResponseDescription(
            operation,
            "403",
            "The authenticated identity reached the endpoint, but its Windows SID could not be " +
            "resolved. The Agent returns application/problem+json with code windows_sid_unavailable.");
        DocumentNegotiateChallenge(operation);
        SetResponseExample(operation, "200", "application/json", new JsonObject
        {
            ["principalName"] = "EXAMPLE\\support-user",
            ["isAuthorized"] = true,
            ["agentVersion"] = "0.0.0",
            ["apiVersion"] = "1.0",
            ["supportedApiVersions"] = new JsonArray("1.0")
        });
        SetResponseExample(operation, "403", "application/problem+json", CreateProblemExample(
            403,
            "The authenticated Windows SID could not be resolved.",
            "windows_sid_unavailable"));
    }

    private static void DocumentMutationToken(OpenApiOperation operation)
    {
        SetOperation(
            operation,
            "Issue a one-use mutation authorization token",
            "Issues a short-lived, one-use token for one server-registered mutation operation. " +
            "Windows Negotiate authentication and local Built-in Administrators membership are " +
            "required; normal browser elevation is not. The request supplies only a logical " +
            "operationId. The server registry resolves the target operation and HTTP method. The " +
            "result is bound to the authenticated Windows SID, exact Support Hub Origin, target " +
            "operation, and server-resolved method, with replay and expiry enforcement. The token is " +
            "header-only, memory-only, and does not itself perform the POS mutation. Production " +
            "registers no feature mutation during this foundation gate, so unknown operations return " +
            "operation_not_supported without issuing a token.");

        if (operation.RequestBody is not null)
        {
            operation.RequestBody.Description =
                "The browser supplies only the server-known logical operationId. It does not supply " +
                "a target path or HTTP method; the Agent's operation registry owns those semantics.";
            SetRequestExample(operation);
        }

        SetResponseDescription(operation, "200", "The Agent issued a short-lived one-use token for the registered operation.");
        SetResponseDescription(
            operation,
            "400",
            "The Agent rejected an unregistered operationId with application/problem+json code " +
            "operation_not_supported; no mutation token is issued.");
        SetResponseDescription(
            operation,
            "401",
            "The Windows authentication middleware issued a Negotiate challenge. This framework " +
            "response is not guaranteed to contain an AgentProblemDetailsDto body; clients should " +
            "inspect the WWW-Authenticate header.");
        SetResponseDescription(
            operation,
            "403",
            "AuthorizationMiddleware may reject a non-Administrator before the endpoint executes; " +
            "that policy-forbid response is bodyless and is not guaranteed to be AgentProblemDetails. " +
            "If the endpoint executes and cannot resolve the authenticated Windows SID, it returns " +
            "application/problem+json with code windows_sid_unavailable.");
        SetResponseDescription(
            operation,
            "429",
            "The bounded in-memory mutation-token retention limit has been reached; the Agent returns " +
            "application/problem+json with code mutation_token_capacity and issues no token.");
        DocumentNegotiateChallenge(operation);
        SetResponseExample(operation, "200", "application/json", new JsonObject
        {
            ["token"] = "opaque-placeholder-not-a-real-token",
            ["expiresAtUtc"] = "2030-01-01T00:00:00Z"
        });
        SetResponseExample(operation, "400", "application/problem+json", CreateProblemExample(
            400,
            "The requested mutation operation is not supported.",
            "operation_not_supported"));
        SetResponseExample(operation, "429", "application/problem+json", CreateProblemExample(
            429,
            "The mutation-token retention limit has been reached.",
            "mutation_token_capacity"));
    }

    private static void SetOperation(
        OpenApiOperation operation,
        string summary,
        string description)
    {
        operation.Summary = summary;
        operation.Description = description;
    }

    private static void SetResponseDescription(
        OpenApiOperation operation,
        string statusCode,
        string description)
    {
        if (operation.Responses is not null
            && operation.Responses.TryGetValue(statusCode, out var response))
        {
            response.Description = description;
        }
    }

    private static void SetRequestExample(OpenApiOperation operation)
    {
        if (operation.RequestBody?.Content is not null
            && operation.RequestBody.Content.TryGetValue("application/json", out var content)
            && content is not null)
        {
            content.Example = new JsonObject
            {
                ["operationId"] = "example.registered-operation"
            };
        }
    }

    private static void DocumentNegotiateChallenge(OpenApiOperation operation)
    {
        if (operation.Responses is null
            || !operation.Responses.TryGetValue("401", out var response)
            || response is not OpenApiResponse openApiResponse)
        {
            return;
        }

        openApiResponse.Content?.Clear();
        openApiResponse.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.OrdinalIgnoreCase);
        openApiResponse.Headers["WWW-Authenticate"] = new OpenApiHeader
        {
            Description = "Negotiate challenge emitted by the Windows authentication middleware.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Example = JsonValue.Create("Negotiate")
            }
        };
    }

    private static void SetResponseExample(
        OpenApiOperation operation,
        string statusCode,
        string mediaType,
        JsonNode example)
    {
        if (operation.Responses is not null
            && operation.Responses.TryGetValue(statusCode, out var response)
            && response?.Content is not null
            && response.Content.TryGetValue(mediaType, out var content)
            && content is not null)
        {
            content.Example = example;
        }
    }

    private static JsonObject CreateProblemExample(int status, string title, string code) => new()
    {
        ["type"] = "about:blank",
        ["title"] = title,
        ["status"] = status,
        ["code"] = code,
        ["correlationId"] = "example-correlation-id"
    };
}
