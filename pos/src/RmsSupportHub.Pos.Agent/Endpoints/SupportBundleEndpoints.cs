using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Agent.Support;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Support;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class SupportBundleEndpoints
{
    public static void MapSupportBundleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/v1/support-bundles",
                async (HttpContext context, SupportBundleRuntime runtime, CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var result = await runtime.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                        return result.SecurityFailure switch
                        {
                            SupportBundleSecurityFailure.PrincipalUnavailable => AgentProblemDetails.CreateResult(
                                context,
                                StatusCodes.Status403Forbidden,
                                "The authenticated Windows SID could not be resolved.",
                                AgentProblemCodes.WindowsSidUnavailable),
                            SupportBundleSecurityFailure.MutationTokenInvalid => AgentProblemDetails.CreateResult(
                                context,
                                StatusCodes.Status403Forbidden,
                                "The mutation request token is invalid.",
                                AgentProblemCodes.MutationTokenInvalid),
                            _ when result.Response is not null => Results.Ok(result.Response),
                            _ => AgentProblemDetails.CreateResult(
                                context,
                                StatusCodes.Status500InternalServerError,
                                "The Support Bundle could not be generated.",
                                "support_bundle_failed")
                        };
                    }
                    catch
                    {
                        return AgentProblemDetails.CreateResult(
                            context,
                            StatusCodes.Status500InternalServerError,
                            "The Support Bundle could not be generated.",
                            "support_bundle_failed");
                    }
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GenerateSupportBundle")
            .WithTags("Diagnostics")
            .WithSummary("Generate a redacted Support Bundle")
            .WithDescription(
                "Generates a bounded local ZIP archive containing typed, redacted Slice A health, " +
                "installation, database, service, failure-analysis, and incident-timeline evidence. " +
                "The operation requires the exact Support Hub Origin and a one-use mutation token, " +
                "returns only an opaque principal-scoped artifact capability, and never includes raw " +
                "configuration, credentials, secrets, filesystem paths, arbitrary logs, or executable " +
                "content. It does not invoke an installer, repair, diagnostic console, or Main Server mutation.")
            .Produces<SupportBundleDto>(StatusCodes.Status200OK, "application/json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");
    }
}
