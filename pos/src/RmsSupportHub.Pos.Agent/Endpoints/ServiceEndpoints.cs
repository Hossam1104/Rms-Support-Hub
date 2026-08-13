using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Agent.Services;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Services;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class ServiceEndpoints
{
    public static void MapServiceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/services",
                async (ReadOnlyServiceStatusService serviceStatus, CancellationToken cancellationToken) =>
                    Results.Ok(await serviceStatus.GetAsync(cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetServices")
            .WithTags("Windows Services")
            .WithSummary("Read allow-listed Windows service visibility")
            .WithDescription(
                "Returns current status evidence for the server-owned, allow-listed Windows services " +
                "configured on the local Agent. Service identifiers are opaque and the response " +
                "contains visibility/status plus only the typed actions valid for the observed state. " +
                "The endpoint has no side effects and never accepts a raw Windows service name.")
            .Produces<IReadOnlyList<ServiceSummaryDto>>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");

        app.MapPost(
                "/api/v1/services/{serviceId}/actions",
                async (
                    HttpContext context,
                    string serviceId,
                    ServiceActionRequestDto? request,
                    ServiceActionRuntime runtime,
                    CancellationToken cancellationToken) =>
                {
                    var result = await runtime.ExecuteAsync(
                        context,
                        serviceId,
                        request,
                        cancellationToken).ConfigureAwait(false);

                    return result.SecurityFailure switch
                    {
                        ServiceActionSecurityFailure.PrincipalUnavailable => AgentProblemDetails.CreateResult(
                            context,
                            StatusCodes.Status403Forbidden,
                            "The authenticated Windows SID could not be resolved.",
                            AgentProblemCodes.WindowsSidUnavailable),
                        ServiceActionSecurityFailure.MutationTokenInvalid => AgentProblemDetails.CreateResult(
                            context,
                            StatusCodes.Status403Forbidden,
                            "The mutation request token is invalid.",
                            AgentProblemCodes.MutationTokenInvalid),
                        _ => Results.Ok(result.Response)
                    };
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("ControlService")
            .WithTags("Windows Services")
            .WithSummary("Start, stop, or restart one allow-listed Windows service")
            .WithDescription(
                "Controls one server-owned, allow-listed Windows service through the typed Start, " +
                "Stop, or Restart contract. The route accepts only an opaque serviceId and a bounded " +
                "idempotency key; raw service names, paths, commands, SQL, scripts, and arbitrary SCM " +
                "input are rejected. Windows Negotiate, server-derived local Built-in Administrators " +
                "membership, the exact Support Hub Origin, and a short-lived one-use mutation token " +
                "bound to this POST path are required. The token is consumed immediately before SCM " +
                "dispatch. The 200 response carries typed outcome truth: NotAttempted means no SCM " +
                "call was made, Accepted means the Agent acknowledged dispatch, Failed means an " +
                "authoritative SCM rejection was classified, and OutcomeUnknown means dispatch or " +
                "cancellation was ambiguous. Unknown outcomes are never retried automatically. The " +
                "response contains only a safe code, detail, and correlation identifier.")
            .Accepts<ServiceActionRequestDto>("application/json")
            .Produces<ServiceActionResponseDto>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");
    }
}
