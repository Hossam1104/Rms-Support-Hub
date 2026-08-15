using RmsSupportHub.Pos.Contracts.V1.Diagnostics;

namespace RmsSupportHub.Pos.Agent.Diagnostics;

/// <summary>Bounded principal-scoped in-memory timeline used for local incident correlation.</summary>
public sealed class IncidentTimelineStore(TimeProvider timeProvider)
{
    private const int MaxEventsPerPrincipal = 256;
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private readonly object gate = new();
    private readonly Dictionary<string, LinkedList<IncidentTimelineEventDto>> events = new(StringComparer.Ordinal);

    public void Record(
        string principalSid,
        string kind,
        FailureSeverity severity,
        string summary,
        string? serviceId = null,
        string? operationId = null,
        string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(principalSid)) return;
        var atUtc = timeProvider.GetUtcNow();
        var item = new IncidentTimelineEventDto(
            Guid.NewGuid().ToString("N"),
            atUtc,
            Safe(kind, 64, "Evidence"),
            severity,
            Safe(summary, 512, "Diagnostic event recorded."),
            SafeIdentifier(serviceId),
            SafeIdentifier(operationId),
            SafeIdentifier(correlationId));

        lock (gate)
        {
            PruneLocked(principalSid, atUtc);
            if (!events.TryGetValue(principalSid, out var list))
            {
                list = new LinkedList<IncidentTimelineEventDto>();
                events[principalSid] = list;
            }

            list.AddFirst(item);
            while (list.Count > MaxEventsPerPrincipal)
            {
                list.RemoveLast();
            }
        }
    }

    public IncidentTimelineDto Get(string principalSid)
    {
        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            PruneLocked(principalSid, now);
            var items = events.TryGetValue(principalSid, out var list)
                ? list.ToArray()
                : [];
            return new(now, items, []);
        }
    }

    private void PruneLocked(string principalSid, DateTimeOffset now)
    {
        if (!events.TryGetValue(principalSid, out var list)) return;
        var cutoff = now - Retention;
        while (list.Last is { } node && node.Value.AtUtc < cutoff)
        {
            list.RemoveLast();
        }

        if (list.Count == 0) events.Remove(principalSid);
    }

    private static string Safe(string? value, int maxLength, string fallback)
    {
        var candidate = new string((value ?? string.Empty)
            .Where(character => !char.IsControl(character))
            .ToArray()).Trim();
        if (candidate.Length == 0) return fallback;
        return candidate.Length <= maxLength ? candidate : candidate[..maxLength];
    }

    private static string? SafeIdentifier(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(character =>
            char.IsControl(character) || char.IsWhiteSpace(character) || character is '/' or '\\' or '?' or '#')
            ? null
            : value;
}
