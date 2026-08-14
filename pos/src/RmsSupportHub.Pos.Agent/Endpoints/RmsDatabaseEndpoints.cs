using System.Text.Json;
using System.Text.Json.Serialization;
using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.RmsDatabase;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Rms;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class RmsDatabaseEndpoints
{
    private static readonly JsonSerializerOptions SseJsonOptions = CreateSseJsonOptions();

    public static void MapRmsDatabaseEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/rms/databases/{targetId}",
                (HttpContext context,
                    string targetId,
                    RmsDatabaseOperationRuntime runtime,
                    IAgentPrincipalSidResolver principalSidResolver) =>
                {
                    if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
                    {
                        return AgentProblemDetails.CreateResult(
                            context,
                            StatusCodes.Status403Forbidden,
                            "The authenticated Windows SID could not be resolved.",
                            AgentProblemCodes.WindowsSidUnavailable);
                    }

                    var workspace = runtime.GetWorkspace(principalSid, targetId);
                    return workspace is null
                        ? Results.NotFound()
                        : Results.Ok(workspace);
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetRmsDatabaseWorkspace")
            .WithTags("RMS Database Operations")
            .WithSummary("Read the typed RMS database workspace")
            .WithDescription(
                "Returns the sanitized status workspace for one server-owned Branch or Cashier " +
                "database target, including approved backup metadata and the latest principal-scoped " +
                "operation. The response contains no connection string, credential, unrestricted " +
                "filesystem path, SQL statement, or raw service target. This read uses Windows " +
                "Negotiate, local Built-in Administrators authorization, and the exact configured " +
                "Support Hub Origin; it does not require a mutation token.")
            .Produces<RmsDatabaseWorkspaceDto>(StatusCodes.Status200OK, "application/json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost(
                "/api/v1/rms/databases/{targetId}/backup",
                async (HttpContext context,
                    string targetId,
                    RmsDatabaseBackupRequestDto? request,
                    RmsDatabaseOperationRuntime runtime,
                    CancellationToken cancellationToken) =>
                {
                    var result = await runtime.ExecuteBackupAsync(
                        context,
                        targetId,
                        request,
                        cancellationToken).ConfigureAwait(false);
                    return ToMutationResult(context, result);
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("BackupRmsDatabase")
            .WithTags("RMS Database Operations")
            .WithSummary("Start a typed RMS database backup")
            .WithDescription(
                "Starts a server-owned backup of exactly the canonical Branch or Cashier RMS " +
                "database. The browser supplies only the logical target, a bounded idempotency key, " +
                "and the one-use mutation token issued for this exact route. The Agent resolves the " +
                "installed connection string, expected database identity, bounded destination, and " +
                "SQL command. The response is typed operation truth and never contains credentials, " +
                "connection strings, arbitrary SQL, or an unrestricted filesystem path. Ambiguous " +
                "dispatches are not retried automatically.")
            .Accepts<RmsDatabaseBackupRequestDto>("application/json")
            .Produces<RmsDatabaseOperationDto>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");

        app.MapPost(
                "/api/v1/rms/databases/{targetId}/restore",
                async (HttpContext context,
                    string targetId,
                    RmsDatabaseRestoreRequestDto? request,
                    RmsDatabaseOperationRuntime runtime,
                    CancellationToken cancellationToken) =>
                {
                    var result = await runtime.ExecuteRestoreAsync(
                        context,
                        targetId,
                        request,
                        cancellationToken).ConfigureAwait(false);
                    return ToMutationResult(context, result);
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("RestoreRmsDatabase")
            .WithTags("RMS Database Operations")
            .WithSummary("Start a confirmed typed RMS database restore")
            .WithDescription(
                "Starts a destructive restore only from an approved Agent-owned backup artifact for " +
                "the selected canonical database. The browser supplies only the logical target, opaque " +
                "artifact ID, exact target-specific confirmation text, bounded idempotency key, and " +
                "one-use mutation token for this exact route. The Agent validates the artifact, " +
                "coordinates only the corresponding allow-listed RMS service, executes bounded native " +
                "SQL, verifies database identity, and attempts service/access recovery. The response " +
                "reports destructive and recovery truth without credentials, SQL, paths, or raw service " +
                "names. Ambiguous outcomes are never retried automatically.")
            .Accepts<RmsDatabaseRestoreRequestDto>("application/json")
            .Produces<RmsDatabaseOperationDto>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");

        app.MapGet(
                "/api/v1/rms/databases/{targetId}/operations/{operationId}",
                (HttpContext context,
                    string targetId,
                    string operationId,
                    RmsDatabaseOperationRuntime runtime,
                    IAgentPrincipalSidResolver principalSidResolver) =>
                {
                    if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
                    {
                        return AgentProblemDetails.CreateResult(
                            context,
                            StatusCodes.Status403Forbidden,
                            "The authenticated Windows SID could not be resolved.",
                            AgentProblemCodes.WindowsSidUnavailable);
                    }

                    return runtime.TryGet(principalSid, targetId, operationId, out var operation)
                        ? Results.Ok(operation)
                        : Results.NotFound();
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetRmsDatabaseOperation")
            .WithTags("RMS Database Operations")
            .WithSummary("Read one principal-scoped RMS database operation")
            .WithDescription(
                "Reads the server-owned state truth for one authenticated administrator's RMS " +
                "database operation. Operation identifiers are opaque and the response contains only " +
                "sanitized progress, artifact metadata, result, correlation, and recovery fields. " +
                "Mutation tokens are not accepted or needed for this read.")
            .Produces<RmsDatabaseOperationDto>(StatusCodes.Status200OK, "application/json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet(
                "/api/v1/rms/databases/{targetId}/operations/{operationId}/events",
                async (HttpContext context,
                    string targetId,
                    string operationId,
                    RmsDatabaseOperationRuntime runtime,
                    IAgentPrincipalSidResolver principalSidResolver) =>
                {
                    if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
                    {
                        await AgentProblemDetails.WriteAsync(
                            context,
                            StatusCodes.Status403Forbidden,
                            "The authenticated Windows SID could not be resolved.",
                            AgentProblemCodes.WindowsSidUnavailable).ConfigureAwait(false);
                        return;
                    }

                    if (!runtime.TryGet(principalSid, targetId, operationId, out _))
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        return;
                    }

                    context.Response.StatusCode = StatusCodes.Status200OK;
                    context.Response.ContentType = "text/event-stream; charset=utf-8";
                    context.Response.Headers.CacheControl = "no-cache";
                    context.Response.Headers["X-Accel-Buffering"] = "no";

                    await foreach (var update in runtime.Stream(
                        principalSid,
                        targetId,
                        operationId,
                        context.RequestAborted).ConfigureAwait(false))
                    {
                        var json = JsonSerializer.Serialize(update, SseJsonOptions);
                        await context.Response.WriteAsync($"data: {json}\n\n", context.RequestAborted)
                            .ConfigureAwait(false);
                        await context.Response.Body.FlushAsync(context.RequestAborted).ConfigureAwait(false);
                    }
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("StreamRmsDatabaseOperationEvents")
            .WithTags("RMS Database Operations")
            .WithSummary("Stream authenticated RMS database operation progress")
            .WithDescription(
                "Streams principal-scoped, read-only progress for one RMS database operation as " +
                "server-sent events. The stream uses the same Windows authentication, administrator " +
                "authorization, exact Origin, and opaque operation lookup as REST state; mutation " +
                "tokens are never placed in a URL or query string.")
            .Produces(StatusCodes.Status200OK, typeof(string), "text/event-stream")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces(StatusCodes.Status404NotFound);
    }

    private static IResult ToMutationResult(
        HttpContext context,
        RmsDatabaseExecutionResult result)
    {
        return result.SecurityFailure switch
        {
            RmsDatabaseSecurityFailure.PrincipalUnavailable => AgentProblemDetails.CreateResult(
                context,
                StatusCodes.Status403Forbidden,
                "The authenticated Windows SID could not be resolved.",
                AgentProblemCodes.WindowsSidUnavailable),
            RmsDatabaseSecurityFailure.MutationTokenInvalid => AgentProblemDetails.CreateResult(
                context,
                StatusCodes.Status403Forbidden,
                "The mutation request token is invalid.",
                AgentProblemCodes.MutationTokenInvalid),
            _ when result.Operation is not null => Results.Ok(result.Operation),
            _ => AgentProblemDetails.CreateResult(
                context,
                result.HttpStatus ?? StatusCodes.Status400BadRequest,
                "The RMS database operation request was rejected.",
                AgentProblemCodes.MutationTargetInvalid)
        };
    }

    private static JsonSerializerOptions CreateSseJsonOptions() => new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
