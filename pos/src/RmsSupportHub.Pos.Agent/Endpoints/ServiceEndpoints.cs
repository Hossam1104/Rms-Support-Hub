using RmsSupportHub.Pos.Agent.Authorization;
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
                "contains visibility/status only. No start, stop, restart, delete, command, or process " +
                "control is registered in this first release; allowedActions is always empty. The " +
                "endpoint has no side effects.")
            .Produces<IReadOnlyList<ServiceSummaryDto>>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");
    }
}
