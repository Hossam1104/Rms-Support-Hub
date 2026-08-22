using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Diagnostics;

/// <summary>
/// Shared, transport-agnostic handler for the safe RMS installation discovery capability.
/// </summary>
public sealed class RmsInstallationDiscoveryQueryHandler(
    IRmsInstallationDiscovery discovery,
    IAgentAuditSink audit,
    TimeProvider timeProvider)
{
    private const string Operation = "rms.installation.discover";

    public async Task<ApplicationResult<RmsInstallationSnapshot>> HandleAsync(
        InvocationContext? context,
        CancellationToken cancellationToken = default)
    {
        var decision = AgentOperationAuthorization.Authorize(
            context,
            AgentOperationRisk.ReadOnlyDiagnostic);

        if (!decision.Allowed)
        {
            RecordAudit(context, "denied", decision.Code);
            return ApplicationResult<RmsInstallationSnapshot>.Failure(decision.Code, decision.Message);
        }

        try
        {
            var result = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
            RecordAudit(context, "completed", null);
            return ApplicationResult<RmsInstallationSnapshot>.Success(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RecordAudit(context, "cancelled", "operation_cancelled");
            throw;
        }
        catch
        {
            RecordAudit(context, "failed", "diagnostic_unavailable");
            return ApplicationResult<RmsInstallationSnapshot>.Failure(
                "diagnostic_unavailable",
                "The RMS installation discovery query could not be completed.");
        }
    }

    private void RecordAudit(InvocationContext? context, string outcome, string? failureCode)
    {
        try
        {
            audit.Record(new AgentAuditEvent(
                timeProvider.GetUtcNow(),
                context?.AuthenticatedCaller ?? "unavailable",
                Operation,
                null,
                context?.CorrelationId ?? "unavailable",
                outcome,
                failureCode,
                "application",
                null)
            {
                Source = context?.Source.ToString()
            });
        }
        catch
        {
            // Audit failure must not turn a read-only query into an unsafe retry signal.
        }
    }
}
