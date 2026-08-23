using RmsSupportHub.Pos.Agent.Artifacts;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;
using RmsSupportHub.Pos.Contracts.V1.Support;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Support;

/// <summary>
/// Transport-neutral Support Bundle coordinator. HTTP and Local IPC perform their own transport
/// security checks, then enter this one executor for generation, audit, and timeline behavior.
/// </summary>
public sealed class SupportBundleExecutor(
    SupportBundleService bundles,
    IncidentTimelineService timeline,
    ArtifactCatalog artifacts,
    IAgentAuditSink audit,
    TimeProvider timeProvider)
{
    public async Task<SupportBundleDto> ExecuteAsync(
        InvocationContext context,
        string principalSid,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var decision = AgentOperationAuthorization.Authorize(
            context,
            AgentOperationRisk.AdministratorOnlyDiagnostic);
        if (!decision.Allowed)
        {
            throw new SupportBundleAuthorizationException(decision.Code);
        }

        if (!IsSafeSid(principalSid)
            || !string.Equals(principalSid, context.AuthenticatedCaller, StringComparison.Ordinal)
            || !IsSafeCorrelation(correlationId))
        {
            throw new SupportBundleAuthorizationException("invocation_context_invalid");
        }

        var response = await bundles
            .GenerateAsync(context, principalSid, correlationId, cancellationToken)
            .ConfigureAwait(false);

        var audited = false;
        try
        {
            audited = audit.Record(new AgentAuditEvent(
                timeProvider.GetUtcNow(),
                principalSid,
                "support-bundle.generate",
                SupportBundleOperation.OperationId,
                correlationId,
                "completed",
                null,
                typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "unavailable",
                null)
            {
                Source = context.Source.ToString()
            });
        }
        catch
        {
            // A failed audit is converted to the same safe unavailable result below.
        }

        if (!audited)
        {
            artifacts.TryRevoke(principalSid, response.Artifact.ArtifactId);
            throw new SupportBundleAuditUnavailableException();
        }

        timeline.Record(
            principalSid,
            "SupportBundle",
            FailureSeverity.Informational,
            "A redacted Support Bundle was generated.",
            operationId: SupportBundleOperation.OperationId,
            correlationId: correlationId);
        return response;
    }

    private static bool IsSafeSid(string? value) =>
        value is { Length: > 0 and <= 184 }
        && value.StartsWith("S-", StringComparison.OrdinalIgnoreCase)
        && value.All(character => char.IsLetterOrDigit(character) || character == '-');

    private static bool IsSafeCorrelation(string? value) =>
        value is { Length: > 0 and <= 128 }
        && value.All(character => character is >= '!' and <= '~');
}

public sealed class SupportBundleAuthorizationException(string code)
    : InvalidOperationException(code);

public sealed class SupportBundleAuditUnavailableException()
    : InvalidOperationException("The Support Bundle audit record was unavailable.");
