using RmsSupportHub.Pos.Contracts.V1.Diagnostics;

namespace RmsSupportHub.Pos.Agent.Diagnostics;

/// <summary>Application-facing facade for the bounded principal-scoped incident timeline.</summary>
public sealed class IncidentTimelineService(IncidentTimelineStore store)
{
    public IncidentTimelineDto Get(string principalSid) => store.Get(principalSid);

    public void Record(
        string principalSid,
        string kind,
        FailureSeverity severity,
        string summary,
        string? serviceId = null,
        string? operationId = null,
        string? correlationId = null) =>
        store.Record(principalSid, kind, severity, summary, serviceId, operationId, correlationId);
}
