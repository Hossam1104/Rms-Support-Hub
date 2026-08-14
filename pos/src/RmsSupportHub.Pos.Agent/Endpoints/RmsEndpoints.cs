using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Rms;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Rms;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class RmsEndpoints
{
    public static void MapRmsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/rms/diagnostics",
                async (RmsDiagnosticsService diagnostics, CancellationToken cancellationToken) =>
                    Results.Ok(await diagnostics.GetAsync(cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetRmsDiagnostics")
            .WithTags("RMS Diagnostics")
            .WithSummary("Discover the installed RMS suite and read safe diagnostics")
            .WithDescription(
                "Reads only the known installed RMS+ metadata and appsettings files, probes the " +
                "configured Branch and Cashier databases with a fixed read-only DB_NAME query, " +
                "checks bounded TCP reachability, and reads the canonical RMS Windows service " +
                "catalog from SCM. The response contains no SQL connection string, password, API " +
                "key, signing key, encryption key, private certificate material, raw filesystem " +
                "path, arbitrary service target, or generic SQL/query surface. Service controls " +
                "continue to use the existing opaque-ID and mutation-token boundary.")
            .Produces<RmsDiagnosticsDto>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");
    }
}
