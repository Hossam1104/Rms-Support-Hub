using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;

namespace RmsSupportHub.Pos.Agent.Diagnostics;

/// <summary>Records safe operation milestones after an authorized POST boundary has run.</summary>
public static class IncidentTimelineRecorder
{
    public static void Record(
        HttpContext context,
        IncidentTimelineService timeline,
        IAgentPrincipalSidResolver principalSidResolver,
        string kind,
        string? outcome,
        string summary,
        string? serviceId = null,
        string? operationId = null)
    {
        if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
        {
            return;
        }

        timeline.Record(
            principalSid,
            kind,
            ToSeverity(outcome),
            summary,
            serviceId,
            operationId,
            CorrelationIdContext.TryGet(context));
    }

    private static FailureSeverity ToSeverity(string? outcome)
    {
        if (string.IsNullOrWhiteSpace(outcome)) return FailureSeverity.Unknown;
        if (outcome.Contains("unknown", StringComparison.OrdinalIgnoreCase)) return FailureSeverity.Unknown;
        if (outcome.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || outcome.Contains("partial", StringComparison.OrdinalIgnoreCase))
        {
            return FailureSeverity.ActionRequired;
        }

        if (outcome.Contains("notattempted", StringComparison.OrdinalIgnoreCase)
            || outcome.Contains("warning", StringComparison.OrdinalIgnoreCase))
        {
            return FailureSeverity.Warning;
        }

        return FailureSeverity.Informational;
    }
}
