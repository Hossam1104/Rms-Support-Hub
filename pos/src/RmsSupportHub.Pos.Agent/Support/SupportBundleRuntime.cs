using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;
using RmsSupportHub.Pos.Contracts.V1.Support;

namespace RmsSupportHub.Pos.Agent.Support;

public static class SupportBundleOperation
{
    public const string OperationId = "support.bundle.generate";
    public const string HttpMethod = "POST";
    public const string HttpPath = "/api/v1/support-bundles";

    public static MutationOperationDescriptor Descriptor { get; } =
        new(OperationId, HttpMethod, HttpPath, MutationTargetKind.None);
}

public enum SupportBundleSecurityFailure
{
    None,
    PrincipalUnavailable,
    MutationTokenInvalid
}

public sealed record SupportBundleExecutionResult(
    SupportBundleDto? Response,
    SupportBundleSecurityFailure SecurityFailure = SupportBundleSecurityFailure.None);

/// <summary>Applies the exact one-use token boundary before generating a local Support Bundle.</summary>
public sealed class SupportBundleRuntime(
    SupportBundleService bundles,
    IncidentTimelineService timeline,
    IMutationTokenStore mutationTokens,
    IAgentPrincipalSidResolver principalSidResolver,
    AgentSecurityOptions securityOptions)
{
    public async Task<SupportBundleExecutionResult> ExecuteAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
        {
            return new(null, SupportBundleSecurityFailure.PrincipalUnavailable);
        }

        var token = context.Request.Headers[MutationTokenContract.HeaderName];
        var origin = context.Request.Headers.Origin;
        if (token.Count != 1
            || origin.Count != 1
            || string.IsNullOrWhiteSpace(token[0])
            || !securityOptions.IsAllowedOrigin(origin[0]))
        {
            return new(null, SupportBundleSecurityFailure.MutationTokenInvalid);
        }

        if (!string.Equals(context.Request.Method, SupportBundleOperation.HttpMethod, StringComparison.Ordinal)
            || !string.Equals(context.Request.Path.Value, SupportBundleOperation.HttpPath, StringComparison.Ordinal))
        {
            return new(null, SupportBundleSecurityFailure.MutationTokenInvalid);
        }

        var consumed = mutationTokens.TryConsume(new MutationTokenValidationRequest(
            token[0]!,
            principalSid,
            origin[0]!,
            context.Request.Method,
            SupportBundleOperation.OperationId,
            context.Request.Path.Value));
        if (!consumed.Succeeded)
        {
            return new(null, SupportBundleSecurityFailure.MutationTokenInvalid);
        }

        var correlationId = CorrelationIdContext.TryGet(context) ?? "unavailable";
        var response = await bundles.GenerateAsync(principalSid, correlationId, cancellationToken).ConfigureAwait(false);
        timeline.Record(
            principalSid,
            "SupportBundle",
            FailureSeverity.Informational,
            "A redacted Support Bundle was generated.",
            operationId: SupportBundleOperation.OperationId,
            correlationId: correlationId);
        return new(response);
    }
}
