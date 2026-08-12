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
            "Windows Negotiate did not authenticate the request.");
        SetResponseDescription(
            operation,
            "403",
            "The authenticated identity does not expose a resolvable Windows SID.");
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
        }

        SetResponseDescription(operation, "200", "The Agent issued a short-lived one-use token for the registered operation.");
        SetResponseDescription(
            operation,
            "400",
            "The logical operationId is not registered by the Agent; no mutation token is issued.");
        SetResponseDescription(
            operation,
            "401",
            "Windows Negotiate did not authenticate the request.");
        SetResponseDescription(
            operation,
            "403",
            "The authenticated Windows identity is not a member of the local Built-in Administrators " +
            "group, or its Windows SID cannot be resolved.");
        SetResponseDescription(
            operation,
            "429",
            "The bounded in-memory mutation-token retention limit has been reached; no token was issued.");
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
}
