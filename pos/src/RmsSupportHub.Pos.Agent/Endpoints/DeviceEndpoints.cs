using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Device;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Device;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var device = app.MapGroup("/api/v1/device")
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithTags("Device");

        device.MapGet(
                "/identity",
                async (DeviceDiagnosticsService diagnostics, CancellationToken cancellationToken) =>
                    Results.Ok(await diagnostics.GetIdentityAsync(cancellationToken).ConfigureAwait(false)))
            .WithName("GetDeviceIdentity")
            .WithSummary("Read safe POS device identity")
            .WithDescription(
                "Returns the Agent's server-owned branch, POS, release, and client identity for the " +
                "local device. Windows Negotiate and local Built-in Administrators membership are " +
                "required. The response has no side effects and never includes a Windows SID, " +
                "credential, or unrestricted host path.")
            .Produces<DeviceIdentityDto>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");

        device.MapGet(
                "/connectivity",
                async (DeviceDiagnosticsService diagnostics, CancellationToken cancellationToken) =>
                    Results.Ok(await diagnostics.GetConnectivityAsync(cancellationToken).ConfigureAwait(false)))
            .WithName("GetDeviceConnectivity")
            .WithSummary("Read local POS connectivity evidence")
            .WithDescription(
                "Performs bounded, read-only TCP reachability checks for the configured local SQL " +
                "endpoint and main-server address. Each evidence node carries its own freshness and " +
                "safe detail; a reachable TCP port is not a claim that SQL or the application is " +
                "healthy. Windows Negotiate and local Built-in Administrators membership are required.")
            .Produces<DeviceConnectivityDto>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");

        device.MapGet(
                "/capabilities",
                (DeviceDiagnosticsService diagnostics) => Results.Ok(diagnostics.GetCapabilities()))
            .WithName("GetDeviceCapabilities")
            .WithSummary("Read safe Agent capabilities")
            .WithDescription(
                "Returns non-secret capability metadata for the installed Agent, including its " +
                "contract version and operating-system label. Browse-root entries, when present, " +
                "contain display metadata only; host paths remain server-owned. This endpoint " +
                "publishes no file-browse or unrelated mutation capability and has no side effects.")
            .Produces<DeviceCapabilitiesDto>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");
    }
}
