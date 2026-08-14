using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.RmsDatabase;

/// <summary>
/// Bounded privileged audit buffer for the local Agent process. It is deliberately not exposed as
/// a browser endpoint; an eventual durable audit adapter can consume the same domain port.
/// </summary>
public sealed class InMemoryRmsAuditSink(
    TimeProvider clock,
    RuntimeRetentionPolicy retention) : IRmsPrivilegedAuditSink
{
    private readonly object gate = new();
    private readonly List<RmsPrivilegedAuditEvent> events = [];

    public void Record(RmsPrivilegedAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        lock (gate)
        {
            events.Add(auditEvent with
            {
                AtUtc = auditEvent.AtUtc == default ? clock.GetUtcNow() : auditEvent.AtUtc,
                CorrelationId = Safe(auditEvent.CorrelationId, "unavailable"),
                PrincipalSid = Safe(auditEvent.PrincipalSid, "unavailable"),
                Detail = Safe(auditEvent.Detail, "Database operation state changed.")
            });
            if (events.Count > retention.MaxActivityEntries)
            {
                events.RemoveRange(0, events.Count - retention.MaxActivityEntries);
            }
        }
    }

    internal IReadOnlyList<RmsPrivilegedAuditEvent> Snapshot()
    {
        lock (gate)
        {
            return events.ToArray();
        }
    }

    private static string Safe(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 512 || value.Any(char.IsControl)
            ? fallback
            : value;
}
