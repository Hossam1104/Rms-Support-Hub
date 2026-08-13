using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace RmsSupportHub.Pos.Agent;

/// <summary>
/// Supplies semantic operation and response descriptions for the current Agent API. Endpoint
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
            case ("GET", "/api/v1/device/identity"):
                DocumentDeviceIdentity(operation);
                break;
            case ("GET", "/api/v1/device/connectivity"):
                DocumentDeviceConnectivity(operation);
                break;
            case ("GET", "/api/v1/device/capabilities"):
                DocumentDeviceCapabilities(operation);
                break;
            case ("GET", "/api/v1/configuration"):
                DocumentConfiguration(operation);
                break;
            case ("GET", "/api/v1/services"):
                DocumentServices(operation);
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
        DocumentTransportBadRequest(operation);
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
        DocumentTransportBadRequest(operation);
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
            "resolved. The Agent returns application/problem+json with code windows_sid_unavailable. " +
            "The exact-origin transport gate may also reject a browser origin with code origin_rejected.");
        DocumentTransportBadRequest(operation);
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
            "operation_not_supported; no mutation token is issued. The host and HTTPS middleware " +
            "may also return host_rejected or https_required in this transport response.");
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
            "application/problem+json with code windows_sid_unavailable. The exact-origin transport " +
            "gate may reject an untrusted browser origin with code origin_rejected.");
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

    private static void DocumentDeviceIdentity(OpenApiOperation operation)
    {
        DocumentProtectedRead(
            operation,
            "Read safe POS device identity",
            "Returns the Agent's server-owned branch, POS, release, and client identity for the " +
            "local device. The response has no side effects and never includes a Windows SID, " +
            "credential, or unrestricted host path.",
            "The Agent returned the safe server-owned DeviceIdentityDto identity.",
            new JsonObject
            {
                ["branchCode"] = "BR-001",
                ["posNumber"] = "POS-01",
                ["release"] = "2026.08",
                ["clientName"] = "RMS+"
            });
    }

    private static void DocumentDeviceConnectivity(OpenApiOperation operation)
    {
        DocumentProtectedRead(
            operation,
            "Read local POS connectivity evidence",
            "Performs bounded, read-only TCP reachability checks for the configured local SQL " +
            "endpoint and main-server address. Each evidence node carries its own freshness and " +
            "safe detail; reachable TCP is not a claim that SQL or the application is healthy.",
            "The Agent returned independent local SQL and main-server EvidenceDto nodes.",
            new JsonObject
            {
                ["localSql"] = EvidenceExample("SQL endpoint is reachable; database health was not queried."),
                ["mainServer"] = EvidenceExample("Main-server TCP endpoint is reachable; application health was not queried.")
            });
    }

    private static void DocumentDeviceCapabilities(OpenApiOperation operation)
    {
        DocumentProtectedRead(
            operation,
            "Read safe Agent capabilities",
            "Returns non-secret capability metadata for the installed Agent, including its " +
            "contract version and operating-system label. Browse-root entries contain display " +
            "metadata only; host paths remain server-owned. This first release publishes no " +
            "file-browse or mutation capability.",
            "The Agent returned safe DeviceCapabilitiesDto metadata.",
            new JsonObject
            {
                ["agentVersion"] = "0.0.0",
                ["operatingSystem"] = "Windows",
                ["browseRoots"] = new JsonArray()
            });
    }

    private static void DocumentConfiguration(OpenApiOperation operation)
    {
        DocumentProtectedRead(
            operation,
            "Read redacted POS configuration",
            "Returns the service-owned POS configuration read model for the local device. Password " +
            "values are represented only by secret-presence flags; SQL/RDB passwords, protected " +
            "data, backup paths, configuration source paths, and unrestricted host paths are never " +
            "returned. This GET does not perform configuration mutation.",
            "The Agent returned the redacted configuration read model without secret values.",
            new JsonObject
            {
                ["sqlInstance"] = "localhost",
                ["sqlUser"] = "agent-user",
                ["hasSqlPassword"] = false,
                ["branchCode"] = "BR-001",
                ["posNumber"] = "POS-01",
                ["release"] = "2026.08",
                ["clientName"] = "RMS+",
                ["apiBaseUrl"] = "https://rms-api.test",
                ["databases"] = new JsonArray("RmsBranchSrv"),
                ["services"] = new JsonArray("RMS.BranchService"),
                ["downloader"] = new JsonObject
                {
                    ["apiUrl"] = "https://rms-downloader.test",
                    ["rdbServerIp"] = "192.0.2.10",
                    ["rdbUsername"] = "agent-reader",
                    ["hasRdbPassword"] = false,
                    ["knownBranchCodes"] = new JsonArray("BR-001"),
                    ["pollIntervalSeconds"] = 5,
                    ["timeoutSeconds"] = 1800
                },
                ["version"] = 1
            });
    }

    private static void DocumentServices(OpenApiOperation operation)
    {
        DocumentProtectedRead(
            operation,
            "Read allow-listed Windows service visibility",
            "Returns current status evidence for the server-owned, allow-listed Windows services " +
            "configured on the local Agent. Service identifiers are opaque and the response contains " +
            "visibility/status only. No start, stop, restart, delete, command, or process control is " +
            "registered in this first release; allowedActions is always empty.",
            "The Agent returned allow-listed service status summaries with no mutation actions.",
            new JsonArray
            {
                new JsonObject
                {
                    ["serviceId"] = "svc-example-opaque",
                    ["displayName"] = "RMS.BranchService",
                    ["state"] = "running",
                    ["lastChecked"] = EvidenceExample("Windows service is running."),
                    ["allowedActions"] = new JsonArray(),
                    ["lastOutcome"] = null
                }
            });
    }

    private static void DocumentProtectedRead(
        OpenApiOperation operation,
        string summary,
        string description,
        string responseDescription,
        JsonNode example)
    {
        SetOperation(operation, summary, description);
        SetResponseDescription(operation, "200", responseDescription);
        SetResponseDescription(
            operation,
            "400",
            "The Agent rejected a non-canonical host with host_rejected or a non-HTTPS request with " +
            "https_required; the response uses the safe Agent problem-details contract.");
        SetResponseDescription(
            operation,
            "401",
            "The Windows authentication middleware issued a Negotiate challenge. This framework " +
            "response is bodyless and is not guaranteed to contain Agent problem details.");
        SetResponseDescription(
            operation,
            "403",
            "AuthorizationMiddleware may reject a non-Administrator with a bodyless response. If the " +
            "exact-origin transport gate rejects the browser origin, the safe problem code is " +
            "origin_rejected.");
        SetResponseDescription(
            operation,
            "500",
            "The Agent failed while reading a server-owned dependency and returned a safe generic " +
            "server-error response without exception details.");
        DocumentNegotiateChallenge(operation);
        SetResponseExample(operation, "200", "application/json", example);
        SetResponseExample(operation, "400", "application/problem+json", CreateProblemExample(
            400,
            "The request host is not accepted.",
            "host_rejected"));
    }

    private static JsonObject EvidenceExample(string detail) => new()
    {
        ["freshness"] = "fresh",
        ["lastCheckedUtc"] = "2030-01-01T00:00:00Z",
        ["detail"] = detail
    };

    private static void DocumentTransportBadRequest(OpenApiOperation operation)
    {
        SetResponseDescription(
            operation,
            "400",
            "The Agent rejected a non-canonical host with host_rejected or a non-HTTPS request with " +
            "https_required; the response uses the safe Agent problem-details contract.");
        SetResponseExample(operation, "400", "application/problem+json", CreateProblemExample(
            400,
            "The request host is not accepted.",
            "host_rejected"));
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
