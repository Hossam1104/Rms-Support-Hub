using RmsSupportHub.Pos.Agent.Artifacts;
using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Artifacts;
using RmsSupportHub.Pos.Contracts.V1.Common;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class ArtifactEndpoints
{
    public static void MapArtifactEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/artifacts/{artifactId}",
                async (HttpContext context,
                    string artifactId,
                    ArtifactCatalog catalog,
                    IAgentPrincipalSidResolver principalSidResolver,
                    CancellationToken cancellationToken) =>
                {
                    if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
                    {
                        return AgentProblemDetails.CreateResult(
                            context,
                            StatusCodes.Status403Forbidden,
                            "The authenticated Windows SID could not be resolved.",
                            AgentProblemCodes.WindowsSidUnavailable);
                    }

                    if (!IsOpaqueArtifactId(artifactId)
                        || !catalog.TryGet(principalSid, artifactId, out ArtifactMetadataDto? metadata)
                        || metadata is null)
                    {
                        return Results.NotFound();
                    }

                    var stream = await catalog.OpenReadAsync(principalSid, artifactId, cancellationToken).ConfigureAwait(false);
                    return stream is null
                        ? Results.NotFound()
                        : Results.File(
                            stream,
                            ContentTypeFor(metadata.DisplayName),
                            metadata.DisplayName,
                            enableRangeProcessing: false);
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("DownloadArtifact")
            .WithTags("Artifacts")
            .WithSummary("Download one principal-scoped Agent artifact")
            .WithDescription(
                "Downloads a server-produced artifact using only its opaque capability identifier. " +
                "The capability is principal-scoped, bounded by in-memory retention, and expires; " +
                "missing, expired, wrong-principal, malformed, or externally removed artifacts return " +
                "404. Content-Disposition is generated from a sanitized catalog display name and no " +
                "filesystem path is accepted or returned.")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces(StatusCodes.Status404NotFound);
    }

    private static bool IsOpaqueArtifactId(string? value) =>
        value is { Length: 32 }
        && value.All(character => Uri.IsHexDigit(character));

    private static string ContentTypeFor(string displayName) =>
        displayName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? "application/zip"
            : "application/octet-stream";
}
