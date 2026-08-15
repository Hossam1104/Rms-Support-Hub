using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Packages;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Packages;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class AgentPackageEndpoints
{
    public static void MapAgentPackageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/packages/status",
                async (AgentPackageService service, CancellationToken cancellationToken) =>
                    Results.Ok(await service.GetStatusAsync(cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetAgentPackageStatus")
            .WithTags("Agent Package")
            .WithSummary("Read the server-owned Agent package boundary")
            .WithDescription("Returns only safe installed-manifest metadata and truthful verification state. Package paths, archives, private keys, and installer arguments are never exposed.")
            .Produces<AgentPackageStatusDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapPost(
                "/api/v1/packages/preview/{operationId}",
                async (
                    HttpContext context,
                    string operationId,
                    AgentPackagePreviewRequestDto request,
                    AgentPackageService service,
                    IAgentPrincipalSidResolver principalSidResolver,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryMapOperation(operationId, out var operation))
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status400BadRequest, "The package operation is not allow-listed.", "package_operation_invalid");
                    }

                    if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The authenticated Windows SID could not be resolved.", AgentProblemCodes.WindowsSidUnavailable);
                    }

                    try
                    {
                        return Results.Ok(await service.PreviewAsync(principalSid, operation, request.IdempotencyKey, cancellationToken).ConfigureAwait(false));
                    }
                    catch (AgentPackageRejectedException exception)
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status400BadRequest, "The package preview was rejected by the Agent.", exception.Message);
                    }
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("PreviewAgentPackageOperation")
            .WithTags("Agent Package")
            .WithSummary("Preview a typed Agent package operation")
            .WithDescription("Reads only the server-owned package catalog and verifier. It does not stage, activate, uninstall, register, or roll back a package.")
            .Accepts<AgentPackagePreviewRequestDto>("application/json")
            .Produces<AgentPackagePreviewDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapPost(
                AgentPackageOperation.HttpPath,
                async (
                    HttpContext context,
                    AgentPackageOperationRequestDto request,
                    AgentPackageService service,
                    IMutationTokenStore tokens,
                    IAgentPrincipalSidResolver principalSidResolver,
                    AgentSecurityOptions securityOptions,
                    CancellationToken cancellationToken) =>
                {
                    var authorization = MutationAuthorization.TryConsume(context, AgentPackageOperation.Descriptor, tokens, principalSidResolver, securityOptions);
                    if (!authorization.Succeeded)
                    {
                        return authorization.Failure == MutationAuthorizationFailure.PrincipalUnavailable
                            ? AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The authenticated Windows SID could not be resolved.", AgentProblemCodes.WindowsSidUnavailable)
                            : AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The mutation request token is invalid.", AgentProblemCodes.MutationTokenInvalid);
                    }

                    try
                    {
                        return Results.Ok(await service.StartAsync(
                            authorization.PrincipalSid!,
                            request,
                            CorrelationIdContext.TryGet(context) ?? "unavailable",
                            cancellationToken).ConfigureAwait(false));
                    }
                    catch (AgentPackageRejectedException exception)
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status400BadRequest, "The Agent package operation was rejected.", exception.Message);
                    }
                    catch
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status500InternalServerError, "The Agent package operation could not be started.", "agent_package_failed");
                    }
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("StartAgentPackageOperation")
            .WithTags("Agent Package")
            .WithSummary("Start an authorized typed Agent package operation")
            .WithDescription(
                "Requires a verified server-owned preview, fresh principal-scoped safety snapshot for " +
                "state-changing operations, exact typed confirmation, exact-origin authentication, " +
                "a one-use mutation token, and idempotency. The package lifecycle boundary reports " +
                "activation, health, rollback, and recovery truth without accepting installer paths or arguments.")
            .Accepts<AgentPackageOperationRequestDto>("application/json")
            .Produces<AgentPackageOperationDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");

        app.MapGet(
                "/api/v1/packages/operations/{operationId}",
                (
                    HttpContext context,
                    string operationId,
                    AgentPackageService service,
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
            .WithName("GetAgentPackageOperation")
            .WithTags("Agent Package")
            .WithSummary("Read a principal-scoped Agent package operation")
            .WithDescription("Returns typed lifecycle state, bounded progress, rollback truth, recovery truth, and safe detail only.")
            .Produces<AgentPackageOperationDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");
    }

    private static bool TryMapOperation(string value, out AgentPackageOperationKind operation) =>
        Enum.TryParse(value, ignoreCase: true, out operation) && Enum.IsDefined(operation);
}
