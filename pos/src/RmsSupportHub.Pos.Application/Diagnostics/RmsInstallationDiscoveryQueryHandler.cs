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
            return RecordAudit(context, "denied", decision.Code)
                ? ApplicationResult<RmsInstallationSnapshot>.Failure(decision.Code, decision.Message)
                : ApplicationResult<RmsInstallationSnapshot>.Failure(
                    "audit_unavailable",
                    "The diagnostic query could not be completed because its audit record was unavailable.");
        }

        try
        {
            var result = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
            return RecordAudit(context, "completed", null)
                ? ApplicationResult<RmsInstallationSnapshot>.Success(result)
                : ApplicationResult<RmsInstallationSnapshot>.Failure(
                    "audit_unavailable",
                    "The diagnostic query could not be completed because its audit record was unavailable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _ = RecordAudit(context, "cancelled", "operation_cancelled");
            throw;
        }
        catch
        {
            return RecordAudit(context, "failed", "diagnostic_unavailable")
                ? ApplicationResult<RmsInstallationSnapshot>.Failure(
                    "diagnostic_unavailable",
                    "The RMS installation discovery query could not be completed.")
                : ApplicationResult<RmsInstallationSnapshot>.Failure(
                    "audit_unavailable",
                    "The diagnostic query could not be completed because its audit record was unavailable.");
        }
    }

    private bool RecordAudit(InvocationContext? context, string outcome, string? failureCode)
    {
        try
        {
            return audit.Record(new AgentAuditEvent(
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
            return false;
        }
    }
}
