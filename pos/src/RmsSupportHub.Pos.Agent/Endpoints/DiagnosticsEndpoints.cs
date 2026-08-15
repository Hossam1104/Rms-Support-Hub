using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class DiagnosticsEndpoints
{
    public static void MapDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/diagnostics/services/{serviceId}/failure",
                async (
                    string serviceId,
                    ServiceFailureAnalyzer analyzer,
                    CancellationToken cancellationToken) =>
                {
                    var result = await analyzer.AnalyzeAsync(serviceId, cancellationToken).ConfigureAwait(false);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetServiceFailureAnalysis")
            .WithTags("Diagnostics")
            .WithSummary("Analyze bounded evidence for an RMS service")
            .WithDescription(
                "Reads the current state of one opaque, allow-listed RMS service identifier and " +
                "correlates it with bounded fixed-root logs and allow-listed Windows event IDs. The " +
                "response includes only redacted exception and stack evidence, safe classifications, " +
                "and non-executing recommendations; it never runs a command, starts a process, " +
                "accepts a path or query, or changes Main Server state.")
            .Produces<ServiceFailureAnalysisDto>(StatusCodes.Status200OK, "application/json")
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");
    }
}
