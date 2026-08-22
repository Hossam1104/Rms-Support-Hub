using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Agent.Snapshots;
using RmsSupportHub.Pos.Agent.Invocation;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Snapshots;

namespace RmsSupportHub.Pos.Agent.Endpoints;

public static class SafetySnapshotEndpoints
{
    public static void MapSafetySnapshotEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/v1/safety-snapshots/preview",
                async (
                    HttpContext context,
                    IAgentInvocationContextFactory contextFactory,
                    SafetySnapshotService service,
                    CancellationToken cancellationToken) =>
                    Results.Ok(await service
                        .PreviewAsync(contextFactory.CreateLegacyLoopback(context), cancellationToken)
                        .ConfigureAwait(false)))
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("PreviewSafetySnapshot")
            .WithTags("Safety Snapshot")
            .WithSummary("Preview a pre-maintenance safety snapshot")
            .WithDescription(
                "Reads bounded safe identity, service, database, fixed-root capacity, backup metadata, " +
                "and configuration fingerprints. Credentials, raw configuration, SQL, logs, private " +
                "keys, arbitrary paths, and full events are excluded.")
            .Produces<SafetySnapshotPreviewDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapPost(
                SafetySnapshotOperation.CaptureHttpPath,
                async (
                    HttpContext context,
                    SafetySnapshotCaptureRequestDto request,
                     SafetySnapshotService service,
                     IMutationTokenStore tokens,
                     IAgentPrincipalSidResolver principalSidResolver,
                     IAgentInvocationContextFactory contextFactory,
                    AgentSecurityOptions securityOptions,
                    CancellationToken cancellationToken) =>
                {
                    var authorization = MutationAuthorization.TryConsume(
                        context,
                        SafetySnapshotOperation.Descriptor,
                        tokens,
                        principalSidResolver,
                        securityOptions);
                    if (!authorization.Succeeded)
                    {
                        return authorization.Failure == MutationAuthorizationFailure.PrincipalUnavailable
                            ? AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The authenticated Windows SID could not be resolved.", AgentProblemCodes.WindowsSidUnavailable)
                            : AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The mutation request token is invalid.", AgentProblemCodes.MutationTokenInvalid);
                    }

                    try
                    {
                        var response = await service.CaptureAsync(
                            contextFactory.CreateLegacyLoopback(context),
                            authorization.PrincipalSid!,
                            request.TypedConfirmation,
                            CorrelationIdContext.TryGet(context) ?? "unavailable",
                            request.IdempotencyKey,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                        return Results.Ok(response);
                    }
                    catch (SafetySnapshotRejectedException exception)
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status400BadRequest, "The safety snapshot request was rejected by the Agent.", exception.Message);
                    }
                    catch
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status500InternalServerError, "The safety snapshot could not be captured safely.", "safety_snapshot_failed");
                    }
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("CaptureSafetySnapshot")
            .WithTags("Safety Snapshot")
            .WithSummary("Capture an atomic principal-scoped safety snapshot")
            .WithDescription(
                "Captures only the bounded evidence shown by the preview after exact-origin, local " +
                "administrator, one-use mutation-token, typed-confirmation, and idempotency checks. " +
                "The snapshot is integrity-protected, expires, and cannot authorize another principal.")
            .Accepts<SafetySnapshotCaptureRequestDto>("application/json")
            .Produces<SafetySnapshotDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status500InternalServerError, "application/problem+json");

        app.MapGet(
                "/api/v1/safety-snapshots/{snapshotId}/verify",
                async (
                    HttpContext context,
                    string snapshotId,
                    SafetySnapshotService service,
                    IAgentPrincipalSidResolver principalSidResolver,
                    CancellationToken cancellationToken) =>
                {
                    if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The authenticated Windows SID could not be resolved.", AgentProblemCodes.WindowsSidUnavailable);
                    }

                    return Results.Ok(await service.VerifyAsync(principalSid, snapshotId, cancellationToken).ConfigureAwait(false));
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("VerifySafetySnapshot")
            .WithTags("Safety Snapshot")
            .WithSummary("Verify a principal-scoped safety snapshot")
            .WithDescription("Rechecks the opaque snapshot identifier, principal binding, expiry, bounded size, and integrity hash before it can authorize repair.")
            .Produces<SafetySnapshotVerificationDto>(StatusCodes.Status200OK)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

        app.MapGet(
                "/api/v1/safety-snapshots/{snapshotId}",
                async (
                    HttpContext context,
                    string snapshotId,
                    SafetySnapshotService service,
                    IAgentPrincipalSidResolver principalSidResolver,
                    CancellationToken cancellationToken) =>
                {
                    if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
                    {
                        return AgentProblemDetails.CreateResult(context, StatusCodes.Status403Forbidden, "The authenticated Windows SID could not be resolved.", AgentProblemCodes.WindowsSidUnavailable);
                    }

                    var response = await service.GetAsync(principalSid, snapshotId, cancellationToken).ConfigureAwait(false);
                    return response is null ? Results.NotFound() : Results.Ok(response);
                })
            .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
            .WithName("GetSafetySnapshot")
            .WithTags("Safety Snapshot")
            .WithSummary("Read a verified safety snapshot projection")
            .WithDescription("Returns only a safe, principal-scoped snapshot projection; invalid, mismatched, corrupt, and unavailable records fail closed.")
            .Produces<SafetySnapshotDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");
    }
}
