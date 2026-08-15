using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Console;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Console;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class DiagnosticConsoleEndpoints
{
    public static void MapDiagnosticConsoleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/v1/diagnostic-console/preview/{targetId}",
                async (
                    HttpContext context,
                    string targetId,
                    DiagnosticConsolePreviewRequestDto request,
                    DiagnosticConsoleRuntime runtime,
                    IAgentPrincipalSidResolver principalSidResolver,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryMapTarget(targetId, out var target))
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status400BadRequest, "The diagnostic target is not allow-listed.", "console_target_invalid");
                    }

                    if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The authenticated Windows SID could not be resolved.", AgentProblemCodes.WindowsSidUnavailable);
                    }

                    try
                    {
                        return Results.Ok(await runtime.PreviewAsync(principalSid, target, request.IdempotencyKey, cancellationToken).ConfigureAwait(false));
                    }
                    catch (DiagnosticConsoleRejectedException exception)
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status400BadRequest, "The diagnostic preview was rejected by the Agent.", exception.Message);
                    }
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("PreviewDiagnosticConsoleRun")
            .WithTags("Diagnostics")
            .WithSummary("Preview a fixed diagnostic console run")
            .WithDescription(
                "Previews only the four fixed RMS executable manifest entries. The browser supplies a " +
                "logical target identifier; executable name, arguments, working directory, child " +
                "environment, timeout, and output bounds are resolved by the Agent and never accepted " +
                "from the request.")
            .Accepts<DiagnosticConsolePreviewRequestDto>("application/json")
            .Produces<DiagnosticConsolePreviewDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapPost(
                DiagnosticConsoleOperation.HttpPath,
                async (
                    HttpContext context,
                    DiagnosticConsoleStartRequestDto request,
                    DiagnosticConsoleRuntime runtime,
                    IMutationTokenStore tokens,
                    IAgentPrincipalSidResolver principalSidResolver,
                    AgentSecurityOptions securityOptions,
                    CancellationToken cancellationToken) =>
                {
                    var authorization = MutationAuthorization.TryConsume(
                        context,
                        DiagnosticConsoleOperation.Descriptor,
                        tokens,
                        principalSidResolver,
                        securityOptions);
                    if (!authorization.Succeeded)
                    {
                        return authorization.Failure == MutationAuthorizationFailure.PrincipalUnavailable
                            ? AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The authenticated Windows SID could not be resolved.", AgentProblemCodes.WindowsSidUnavailable)
                            : AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The mutation request token is invalid.", AgentProblemCodes.MutationTokenInvalid);
                    }

                    try
                    {
                        return Results.Ok(await runtime.StartAsync(
                            authorization.PrincipalSid!,
                            request,
                            CorrelationIdContext.TryGet(context) ?? "unavailable",
                            cancellationToken).ConfigureAwait(false));
                    }
                    catch (DiagnosticConsoleRejectedException exception)
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status400BadRequest, "The diagnostic console request was rejected by the Agent.", exception.Message);
                    }
                    catch
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status500InternalServerError, "The diagnostic console operation could not be started.", "diagnostic_console_failed");
                    }
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("StartDiagnosticConsoleRun")
            .WithTags("Diagnostics")
            .WithSummary("Start an authorized fixed diagnostic console run")
            .WithDescription(
                "Starts only the exact executable and argument template represented by a prior Agent " +
                "preview. It requires exact-origin authentication, local administrator authorization, " +
                "a one-use mutation token, typed confirmation, and idempotency. Standard output and " +
                "error are bounded, redacted, stored separately, and exposed only as opaque artifact IDs.")
            .Accepts<DiagnosticConsoleStartRequestDto>("application/json")
            .Produces<DiagnosticConsoleRunDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");

        app.MapGet(
                "/api/v1/diagnostic-console/runs/{operationId}",
                (
                    HttpContext context,
                    string operationId,
                    DiagnosticConsoleRuntime runtime,
                    IAgentPrincipalSidResolver principalSidResolver) =>
                {
                    if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The authenticated Windows SID could not be resolved.", AgentProblemCodes.WindowsSidUnavailable);
                    }

                    return runtime.TryGet(principalSid, operationId, out var response) && response is not null
                        ? Results.Ok(response)
                        : Results.NotFound();
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetDiagnosticConsoleRun")
            .WithTags("Diagnostics")
            .WithSummary("Read a principal-scoped diagnostic console result")
            .WithDescription("Returns only the typed operation state, bounded counts, safe detail, and opaque principal-scoped stdout/stderr artifact capabilities.")
            .Produces<DiagnosticConsoleRunDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");
    }

    private static bool TryMapTarget(string value, out DiagnosticConsoleTarget target) =>
        Enum.TryParse(value, ignoreCase: true, out target)
        && Enum.IsDefined(target);
}
