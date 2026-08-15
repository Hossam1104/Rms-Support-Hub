using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.MainServer;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.MainServer;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class MainServerEndpoints
{
    public static void MapMainServerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/main-server/profiles",
                async (MainServerService service, CancellationToken cancellationToken) =>
                    Results.Ok(await service.GetProfilesAsync(cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetMainServerProfiles")
            .WithTags("Main Server")
            .WithSummary("Read server-owned Main Server profiles")
            .WithDescription(
                "Returns only fixed Testing and future Production profile projections. The browser " +
                "cannot select a URL, host, branch, POS, credential, authorization material, or arbitrary route. " +
                "Production remains disabled until separately approved, and the response contains no " +
                "connection string or authorization material.")
            .Produces<MainServerProfilesDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapGet(
                "/api/v1/main-server/state",
                async (HttpContext context, MainServerService service, CancellationToken cancellationToken) =>
                {
                    var correlationId = CorrelationIdContext.TryGet(context) ?? "unavailable";
                    return Results.Ok(await service.GetStateAsync(correlationId, cancellationToken).ConfigureAwait(false));
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetMainServerState")
            .WithTags("Main Server")
            .WithSummary("Read bound Branch and POS Main Server state")
            .WithDescription(
                "Performs only the Agent-owned, typed GET projections allowed by the active server-owned " +
                "profile after Branch/POS identity binding. It never proxies a generic Main Server API, " +
                "invokes installation-state PUT acknowledgements, or returns secret-bearing installation responses.")
            .Produces<MainServerStateEvidenceDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");
    }
}
