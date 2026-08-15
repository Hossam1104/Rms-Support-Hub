using RmsSupportHub.Pos.Contracts.V1.Common;

namespace RmsSupportHub.Pos.Contracts.V1.Diagnostics;

/// <summary>One principal-scoped, safe timeline event retained by the local Agent.</summary>
public sealed record IncidentTimelineEventDto(
    /// <summary>Opaque timeline event identifier.</summary>
    string EventId,
    /// <summary>UTC event time.</summary>
    DateTimeOffset AtUtc,
    /// <summary>Stable event kind such as HealthCheck, Service, Database, or Evidence.</summary>
    string Kind,
    /// <summary>Safe event severity.</summary>
    FailureSeverity Severity,
    /// <summary>Redacted operator summary.</summary>
    string Summary,
    /// <summary>Opaque service identifier, when applicable.</summary>
    string? ServiceId,
    /// <summary>Safe operation identifier, when applicable.</summary>
    string? OperationId,
    /// <summary>Correlation identifier for cross-surface investigation.</summary>
    string? CorrelationId);

/// <summary>Bounded incident timeline for the authenticated Windows principal.</summary>
public sealed record IncidentTimelineDto(
    /// <summary>UTC generation time.</summary>
    DateTimeOffset GeneratedAtUtc,
    /// <summary>Newest-first bounded timeline events.</summary>
    IReadOnlyList<IncidentTimelineEventDto> Events,
    /// <summary>Safe reasons historical evidence may be incomplete.</summary>
    IReadOnlyList<string> UnknownReasons);
