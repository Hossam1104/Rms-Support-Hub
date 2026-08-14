using System.Text.Json;
using System.Text.Json.Serialization;
using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Runtime;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Maintenance;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class MaintenanceEndpoints
{
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static void MapMaintenanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/v1/maintenance/cleanup/preview",
                async (HttpContext context, MaintenanceOperationRuntime runtime, CancellationToken cancellationToken) =>
                {
                    var result = await runtime.PreviewCleanupAsync(context, cancellationToken).ConfigureAwait(false);
                    return result.SecurityFailure == MaintenanceSecurityFailure.PrincipalUnavailable
                        ? AgentProblemDetails.CreateResult(
                            context,
                            StatusCodes.Status403Forbidden,
                            "The authenticated Windows SID could not be resolved.",
                            AgentProblemCodes.WindowsSidUnavailable)
                        : Results.Ok(result.Response);
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("PreviewMaintenanceCleanup")
            .WithTags("Maintenance")
            .WithSummary("Preview server-owned maintenance cleanup")
            .WithDescription(
                "Builds a fresh, server-owned cleanup impact preview. The browser selects no path " +
                "or service; logical target IDs, policy rejections, bounded evidence, and an expiring " +
                "principal-bound confirmation challenge are returned. Preview has no destructive side " +
                "effect and does not issue a mutation token.")
            .Produces<CleanupPreviewDto>(StatusCodes.Status200OK, "application/json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapPost(
                "/api/v1/maintenance/cleanup/execute",
                async (HttpContext context,
                    CleanupExecuteRequestDto? request,
                    MaintenanceOperationRuntime runtime,
                    CancellationToken cancellationToken) =>
                {
                    var result = await runtime.ExecuteCleanupAsync(context, request, cancellationToken).ConfigureAwait(false);
                    return ToMutationResult(context, result);
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("ExecuteMaintenanceCleanup")
            .WithTags("Maintenance")
            .WithSummary("Execute confirmed server-owned maintenance cleanup")
            .WithDescription(
                "Executes only the exact server-owned cleanup preview represented by the challenge. " +
                "The request must carry the exact confirmation phrase, a bounded idempotency key, " +
                "and a one-use mutation token bound to this exact POST path. The Agent recomputes " +
                "path policy before every delete and reports partial/recovery or unknown outcomes " +
                "without returning paths, service names, credentials, SQL, or exception text.")
            .Accepts<CleanupExecuteRequestDto>("application/json")
            .Produces<MaintenanceOperationDto>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapPost(
                "/api/v1/maintenance/reset/preview",
                async (HttpContext context, MaintenanceOperationRuntime runtime, CancellationToken cancellationToken) =>
                {
                    var result = await runtime.PreviewBranchResetAsync(context, cancellationToken).ConfigureAwait(false);
                    return result.SecurityFailure == MaintenanceSecurityFailure.PrincipalUnavailable
                        ? AgentProblemDetails.CreateResult(
                            context,
                            StatusCodes.Status403Forbidden,
                            "The authenticated Windows SID could not be resolved.",
                            AgentProblemCodes.WindowsSidUnavailable)
                        : Results.Ok(result.Response);
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("PreviewMaintenanceBranchReset")
            .WithTags("Maintenance")
            .WithSummary("Preview server-owned branch reset")
            .WithDescription(
                "Builds a fresh, server-owned branch reset preview for the configured branch and " +
                "code-owned table scope. The browser selects no database, SQL, table, or service; " +
                "an expiring principal-bound challenge is issued only when policy and read-only scope " +
                "verification succeed.")
            .Produces<BranchResetPreviewDto>(StatusCodes.Status200OK, "application/json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapPost(
                "/api/v1/maintenance/reset/execute",
                async (HttpContext context,
                    BranchResetExecuteRequestDto? request,
                    MaintenanceOperationRuntime runtime,
                    CancellationToken cancellationToken) =>
                {
                    var result = await runtime.ExecuteBranchResetAsync(context, request, cancellationToken).ConfigureAwait(false);
                    return ToMutationResult(context, result);
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("ExecuteMaintenanceBranchReset")
            .WithTags("Maintenance")
            .WithSummary("Execute confirmed server-owned branch reset")
            .WithDescription(
                "Executes only the exact server-owned branch reset preview represented by the " +
                "challenge. The request must carry the exact confirmation phrase, bounded " +
                "idempotency, and a one-use mutation token bound to this exact POST path. Only the " +
                "approved database and table scope can cross the typed maintenance database seam; " +
                "partial, recovery-required, and unknown outcomes are retained and never retried " +
                "automatically.")
            .Accepts<BranchResetExecuteRequestDto>("application/json")
            .Produces<MaintenanceOperationDto>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapGet(
                "/api/v1/maintenance/operations/{operationId}",
                (HttpContext context,
                    string operationId,
                    MaintenanceOperationRuntime runtime,
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
            .WithName("GetMaintenanceOperation")
            .WithTags("Maintenance")
            .WithSummary("Read one principal-scoped maintenance operation")
            .WithDescription(
                "Reads sanitized progress, evidence, and recovery truth for one authenticated " +
                "administrator's maintenance operation. Operation and target identifiers are opaque " +
                "or logical; no raw path, SQL, service, credential, or exception text is returned.")
            .Produces<MaintenanceOperationDto>(StatusCodes.Status200OK, "application/json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet(
                "/api/v1/maintenance/operations/{operationId}/events",
                async (HttpContext context,
                    string operationId,
                    MaintenanceOperationRuntime runtime,
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
            .WithName("StreamMaintenanceOperationEvents")
            .WithTags("Maintenance")
            .WithSummary("Stream authenticated maintenance operation progress")
            .WithDescription(
                "Streams principal-scoped, read-only maintenance progress as server-sent events. " +
                "Mutation tokens are never placed in a URL or query string.")
            .Produces(StatusCodes.Status200OK, typeof(string), "text/event-stream")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces(StatusCodes.Status404NotFound);
    }

    private static IResult ToMutationResult(HttpContext context, MaintenanceAgentExecutionResult result) =>
        result.SecurityFailure switch
        {
            MaintenanceSecurityFailure.PrincipalUnavailable => AgentProblemDetails.CreateResult(
                context,
                StatusCodes.Status403Forbidden,
                "The authenticated Windows SID could not be resolved.",
                AgentProblemCodes.WindowsSidUnavailable),
            MaintenanceSecurityFailure.MutationTokenInvalid => AgentProblemDetails.CreateResult(
                context,
                StatusCodes.Status403Forbidden,
                "The mutation request token is invalid.",
                AgentProblemCodes.MutationTokenInvalid),
            _ when result.Response is not null => Results.Ok(result.Response),
            _ => AgentProblemDetails.CreateResult(
                context,
                StatusCodes.Status400BadRequest,
                "The maintenance operation request was rejected.",
                AgentProblemCodes.MutationTargetInvalid)
        };
}
