using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class TimelineEndpoints
{
    public static void MapTimelineEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/diagnostics/timeline",
                (HttpContext context,
                    IncidentTimelineService timeline,
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

                    return Results.Ok(timeline.Get(principalSid));
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetIncidentTimeline")
            .WithTags("Diagnostics")
            .WithSummary("Read the bounded incident timeline")
            .WithDescription(
                "Returns newest-first principal-scoped timeline events retained by this local Agent " +
                "for service, health, database, recovery, download, and maintenance correlation. " +
                "Events contain safe summaries and opaque identifiers only; the endpoint never reads " +
                "arbitrary history, returns raw logs or secrets, accepts query filters, or performs " +
                "a state-changing operation.")
            .Produces<IncidentTimelineDto>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");
    }
}
