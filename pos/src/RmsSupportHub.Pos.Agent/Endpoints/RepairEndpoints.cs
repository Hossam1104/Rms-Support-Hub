using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Repair;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Repair;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class RepairEndpoints
{
    public static void MapRepairEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/repair/preview/{operationId}",
                (HttpContext context, string operationId) =>
                    PreviewAsync(context, operationId, null))
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("PreviewRepairWithoutSnapshot")
            .WithTags("Repair")
            .WithSummary("Preview a typed repair operation")
            .WithDescription("Returns a blocked preview until a fresh opaque safety snapshot identifier is supplied. Health recommendations never trigger repair automatically.")
            .Produces<RepairPreviewDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapGet(
                "/api/v1/repair/preview/{operationId}/{snapshotId}",
                (HttpContext context, string operationId, string snapshotId) =>
                    PreviewAsync(context, operationId, snapshotId))
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("PreviewRepairWithSnapshot")
            .WithTags("Repair")
            .WithSummary("Preview repair after selecting an opaque safety snapshot")
            .WithDescription("Binds the typed repair preview to the authenticated principal and the fresh snapshot identifier; package identity, signature, checksum, capacity, and compatibility are verified server-side.")
            .Produces<RepairPreviewDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapPost(
                RepairOperation.HttpPath,
                async (
                    HttpContext context,
                    RepairExecuteRequestDto request,
                    RepairService service,
                    IMutationTokenStore tokens,
                    IAgentPrincipalSidResolver principalSidResolver,
                    AgentSecurityOptions securityOptions,
                    CancellationToken cancellationToken) =>
                {
                    var authorization = MutationAuthorization.TryConsume(context, RepairOperation.Descriptor, tokens, principalSidResolver, securityOptions);
                    if (!authorization.Succeeded)
                    {
                        return authorization.Failure == MutationAuthorizationFailure.PrincipalUnavailable
                            ? AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The authenticated Windows SID could not be resolved.", AgentProblemCodes.WindowsSidUnavailable)
                            : AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The mutation request token is invalid.", AgentProblemCodes.MutationTokenInvalid);
                    }

                    try
                    {
                        return Results.Ok(await service.ExecuteAsync(
                            authorization.PrincipalSid!,
                            request,
                            CorrelationIdContext.TryGet(context) ?? "unavailable",
                            cancellationToken).ConfigureAwait(false));
                    }
                    catch (RepairRejectedException exception)
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status400BadRequest, "The repair operation was rejected by the Agent.", exception.Message);
                    }
                    catch
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status500InternalServerError, "The repair operation could not be started.", "repair_failed");
                    }
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("StartRepairOperation")
            .WithTags("Repair")
            .WithSummary("Start an authorized typed repair operation")
            .WithDescription(
                "Requires health evidence, a fresh verified safety snapshot, verified package identity, " +
                "capacity preconditions, an explicit preview, exact confirmation, a one-use mutation " +
                "token, and idempotency. The typed Agent package boundary owns activation and reports " +
                "health, rollback, and recovery truth. No Main Server installation acknowledgement is invoked.")
            .Accepts<RepairExecuteRequestDto>("application/json")
            .Produces<RepairOperationDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");

        app.MapGet(
                "/api/v1/repair/operations/{operationId}",
                (
                    HttpContext context,
                    string operationId,
                    RepairService service,
                    IAgentPrincipalSidResolver principalSidResolver) =>
                {
                    if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The authenticated Windows SID could not be resolved.", AgentProblemCodes.WindowsSidUnavailable);
                    }

                    return service.TryGetOperation(principalSid, operationId, out var response) && response is not null
                        ? Results.Ok(response)
                        : Results.NotFound();
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetRepairOperation")
            .WithTags("Repair")
            .WithSummary("Read a principal-scoped repair operation")
            .WithDescription("Returns bounded typed repair state and truthful rollback/recovery results only.")
            .Produces<RepairOperationDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapGet(
                "/api/v1/repair/guided/preview",
                (HttpContext context, RepairService service, IAgentPrincipalSidResolver principalSidResolver, CancellationToken cancellationToken) =>
                    BeginGuidedAsync(context, null, service, principalSidResolver, cancellationToken))
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("PreviewGuidedRepair")
            .WithTags("Repair")
            .WithSummary("Begin a blocked or ready Guided Repair workflow")
            .WithDescription("Creates a principal-scoped fixed checkpoint sequence. It never enables repair from a health recommendation and requires an opaque safety snapshot before advancing.")
            .Produces<GuidedRepairDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapGet(
                "/api/v1/repair/guided/preview/{snapshotId}",
                (HttpContext context, string snapshotId, RepairService service, IAgentPrincipalSidResolver principalSidResolver, CancellationToken cancellationToken) =>
                    BeginGuidedAsync(context, snapshotId, service, principalSidResolver, cancellationToken))
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("PreviewGuidedRepairWithSnapshot")
            .WithTags("Repair")
            .WithSummary("Begin Guided Repair with an opaque safety snapshot")
            .WithDescription("Binds the fixed checkpoint sequence to the authenticated principal and fresh opaque snapshot identifier.")
            .Produces<GuidedRepairDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapGet(
                "/api/v1/repair/guided/{guidedRepairId}",
                (
                    HttpContext context,
                    string guidedRepairId,
                    RepairService service,
                    IAgentPrincipalSidResolver principalSidResolver) =>
                {
                    if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The authenticated Windows SID could not be resolved.", AgentProblemCodes.WindowsSidUnavailable);
                    }

                    return service.TryGetGuided(principalSid, guidedRepairId, out var response) && response is not null
                        ? Results.Ok(response)
                        : Results.NotFound();
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetGuidedRepair")
            .WithTags("Repair")
            .WithSummary("Read a principal-scoped Guided Repair workflow")
            .WithDescription("Returns fixed typed checkpoints and safe blocker state. It contains no shell, installer, path, service, SQL, or Main Server mutation control.")
            .Produces<GuidedRepairDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapPost(
                GuidedRepairOperation.HttpPath,
                async (
                    HttpContext context,
                    GuidedRepairStepRequestDto request,
                    RepairService service,
                    IMutationTokenStore tokens,
                    IAgentPrincipalSidResolver principalSidResolver,
                    AgentSecurityOptions securityOptions,
                    CancellationToken cancellationToken) =>
                {
                    var authorization = MutationAuthorization.TryConsume(context, GuidedRepairOperation.Descriptor, tokens, principalSidResolver, securityOptions);
                    if (!authorization.Succeeded)
                    {
                        return authorization.Failure == MutationAuthorizationFailure.PrincipalUnavailable
                            ? AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The authenticated Windows SID could not be resolved.", AgentProblemCodes.WindowsSidUnavailable)
                            : AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The mutation request token is invalid.", AgentProblemCodes.MutationTokenInvalid);
                    }

                    try
                    {
                        return Results.Ok(await service.AdvanceGuidedAsync(authorization.PrincipalSid!, request, cancellationToken).ConfigureAwait(false));
                    }
                    catch (RepairRejectedException exception)
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status400BadRequest, "The Guided Repair checkpoint was rejected.", exception.Message);
                    }
                    catch
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status500InternalServerError, "The Guided Repair checkpoint could not be evaluated.", "guided_repair_failed");
                    }
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("AdvanceGuidedRepair")
            .WithTags("Repair")
            .WithSummary("Advance one typed Guided Repair checkpoint")
            .WithDescription("Requires exact-origin authentication, a one-use mutation token, exact fixed checkpoint ordering, typed confirmation, and idempotency. Staging or activation is never inferred from a checkpoint click.")
            .Accepts<GuidedRepairStepRequestDto>("application/json")
            .Produces<GuidedRepairDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");
    }

    private static async Task<IResult> PreviewAsync(HttpContext context, string operationId, string? snapshotId)
    {
        if (!TryMapOperation(operationId, out var operation))
        {
            return AgentProblemDetails.CreateResult(context, StatusCodes.Status400BadRequest, "The repair operation is not allow-listed.", "repair_operation_invalid");
        }

        var service = context.RequestServices.GetRequiredService<RepairService>();
        var principalSidResolver = context.RequestServices.GetRequiredService<IAgentPrincipalSidResolver>();
        if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
        {
            return AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The authenticated Windows SID could not be resolved.", AgentProblemCodes.WindowsSidUnavailable);
        }

        return Results.Ok(await service.PreviewAsync(principalSid, operation, snapshotId, context.RequestAborted).ConfigureAwait(false));
    }

    private static async Task<IResult> BeginGuidedAsync(HttpContext context, string? snapshotId, RepairService service, IAgentPrincipalSidResolver principalSidResolver, CancellationToken cancellationToken)
    {
        if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
        {
            return AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The authenticated Windows SID could not be resolved.", AgentProblemCodes.WindowsSidUnavailable);
        }

        return Results.Ok(await service.BeginGuidedAsync(principalSid, snapshotId, cancellationToken).ConfigureAwait(false));
    }

    private static bool TryMapOperation(string value, out RepairOperationKind operation) =>
        Enum.TryParse(value, ignoreCase: true, out operation) && Enum.IsDefined(operation);
}
