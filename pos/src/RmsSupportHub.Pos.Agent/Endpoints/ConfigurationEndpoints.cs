using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Application.UseCases;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Configuration;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class ConfigurationEndpoints
{
    public static void MapConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/configuration",
                async (AgentConfigurationUseCase useCase, CancellationToken cancellationToken) =>
                {
                    var snapshot = await useCase.GetAsync(cancellationToken).ConfigureAwait(false);
                    return Results.Ok(ToDto(snapshot));
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetConfiguration")
            .WithTags("Configuration")
            .WithSummary("Read redacted POS configuration")
            .WithDescription(
                "Returns the service-owned POS configuration read model for the local device. " +
                "Password values are represented only by secret-presence flags; SQL/RDB passwords, " +
                "protected data, backup paths, configuration source paths, and unrestricted host " +
                "paths are never returned. This GET is read-only and does not accept browser-owned " +
                "credentials or perform configuration mutation.")
            .Produces<RedactedConfigurationDto>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");
    }

    private static RedactedConfigurationDto ToDto(AgentConfigurationSnapshot snapshot)
    {
        var configuration = snapshot.Configuration;
        return new(
            configuration.SqlInstance,
            configuration.SqlUser,
            snapshot.HasSqlPassword,
            configuration.BranchCode,
            configuration.PosNumber,
            configuration.Release,
            configuration.ClientName,
            configuration.ApiBaseUrl,
            configuration.Databases,
            configuration.Services,
            new RedactedDownloaderConfigurationDto(
                configuration.Downloader.ApiUrl,
                configuration.Downloader.RdbServerIp,
                configuration.Downloader.RdbUsername,
                snapshot.HasRdbPassword,
                configuration.Downloader.KnownBranchCodes,
                configuration.Downloader.PollIntervalSeconds,
                configuration.Downloader.TimeoutSeconds),
            configuration.Version);
    }
}
