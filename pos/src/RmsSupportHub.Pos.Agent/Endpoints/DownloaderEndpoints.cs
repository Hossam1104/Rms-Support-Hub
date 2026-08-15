using System.Text.Json;
using System.Text.Json.Serialization;
using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Agent.Runtime;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Downloader;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class DownloaderEndpoints
{
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static void MapDownloaderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/downloads/branches",
                async (DownloaderOperationRuntime runtime, CancellationToken cancellationToken) =>
                    Results.Ok(await runtime.GetBranchesAsync(cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetDownloaderBranches")
            .WithTags("Downloader")
            .WithSummary("Read server-approved downloader branches")
            .WithDescription(
                "Returns only the branch identifiers that the Agent has configured for the typed " +
                "backup downloader. SMB paths, remote credentials, endpoint connection details, and " +
                "arbitrary branch selection are never accepted from or returned to the browser.")
            .Produces<IReadOnlyList<BranchCatalogEntryDto>>(StatusCodes.Status200OK, "application/json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        app.MapPost(
                "/api/v1/downloads/batches",
                async (
                    HttpContext context,
                    TriggerBatchRequestDto? request,
                    DownloaderOperationRuntime runtime,
                    IncidentTimelineService timeline,
                    IAgentPrincipalSidResolver principalSidResolver,
                    CancellationToken cancellationToken) =>
                {
                    var result = await runtime.ExecuteAsync(context, request, cancellationToken).ConfigureAwait(false);
                    if (result.Response is { } response)
                    {
                        IncidentTimelineRecorder.Record(
                            context,
                            timeline,
                            principalSidResolver,
                            "DownloaderOperation",
                            response.Outcome.ToString(),
                            response.Detail,
                            operationId: response.OperationId);
                    }

                    return ToMutationResult(context, result);
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("TriggerDownloaderBatch")
            .WithTags("Downloader")
            .WithSummary("Start a typed RMS backup download batch")
            .WithDescription(
                "Starts a server-owned backup trigger and bounded SMB discovery/download for the " +
                "selected configured branches. The request contains only branch codes and an " +
                "idempotency key. The Agent resolves the remote endpoint, SMB root, credential, " +
                "matching policy, local staging root, and artifact publication. A one-use mutation " +
                "token is bound to this exact POST path. Trigger or download uncertainty is retained " +
                "as outcomeUnknown and is never retried automatically; results expose only branch " +
                "state and opaque artifact IDs.")
            .Accepts<TriggerBatchRequestDto>("application/json")
            .Produces<DownloaderOperationDto>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");

        app.MapGet(
                "/api/v1/downloads/operations/{operationId}",
                (HttpContext context,
                    string operationId,
                    DownloaderOperationRuntime runtime,
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

                    return runtime.TryGet(principalSid, operationId, out var operation)
                        ? Results.Ok(operation)
                        : Results.NotFound();
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetDownloaderOperation")
            .WithTags("Downloader")
            .WithSummary("Read one principal-scoped downloader operation")
            .WithDescription(
                "Reads sanitized progress and outcome for one downloader operation owned by the " +
                "authenticated administrator. The operation identifier is opaque, artifacts are " +
                "opaque, and the response never contains remote/local paths, credentials, or raw " +
                "transport errors.")
            .Produces<DownloaderOperationDto>(StatusCodes.Status200OK, "application/json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet(
                "/api/v1/downloads/operations/{operationId}/events",
                async (HttpContext context,
                    string operationId,
                    DownloaderOperationRuntime runtime,
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

                    if (!runtime.TryGet(principalSid, operationId, out _))
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        return;
                    }

                    context.Response.StatusCode = StatusCodes.Status200OK;
                    context.Response.ContentType = "text/event-stream; charset=utf-8";
                    context.Response.Headers.CacheControl = "no-cache";
                    context.Response.Headers["X-Accel-Buffering"] = "no";
                    await foreach (var update in runtime.Stream(principalSid, operationId, context.RequestAborted).ConfigureAwait(false))
                    {
                        var json = JsonSerializer.Serialize(update, SseJsonOptions);
                        await context.Response.WriteAsync($"data: {json}\n\n", context.RequestAborted).ConfigureAwait(false);
                        await context.Response.Body.FlushAsync(context.RequestAborted).ConfigureAwait(false);
                    }
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("StreamDownloaderOperationEvents")
            .WithTags("Downloader")
            .WithSummary("Stream authenticated downloader operation progress")
            .WithDescription(
                "Streams principal-scoped, read-only downloader progress as server-sent events. " +
                "Mutation tokens are never placed in a URL or query string.")
            .Produces(StatusCodes.Status200OK, typeof(string), "text/event-stream")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces(StatusCodes.Status404NotFound);
    }

    private static IResult ToMutationResult(HttpContext context, DownloaderAgentExecutionResult result) =>
        result.SecurityFailure switch
        {
            DownloaderSecurityFailure.PrincipalUnavailable => AgentProblemDetails.CreateResult(
                context,
                StatusCodes.Status403Forbidden,
                "The authenticated Windows SID could not be resolved.",
                AgentProblemCodes.WindowsSidUnavailable),
            DownloaderSecurityFailure.MutationTokenInvalid => AgentProblemDetails.CreateResult(
                context,
                StatusCodes.Status403Forbidden,
                "The mutation request token is invalid.",
                AgentProblemCodes.MutationTokenInvalid),
            _ when result.Response is not null => Results.Ok(result.Response),
            _ => AgentProblemDetails.CreateResult(
                context,
                StatusCodes.Status400BadRequest,
                "The downloader operation request was rejected.",
                AgentProblemCodes.MutationTargetInvalid)
        };
}
