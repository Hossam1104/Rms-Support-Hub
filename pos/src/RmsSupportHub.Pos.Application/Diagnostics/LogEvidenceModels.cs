namespace RmsSupportHub.Pos.Application.Diagnostics;

/// <summary>Server-owned classification for bounded service-failure evidence.</summary>
public enum FailureCategory
{
    None,
    ServiceStopped,
    ServiceStartFailure,
    Crash,
    Database,
    Network,
    Configuration,
    VersionDrift,
    Unknown
}

/// <summary>Conservative severity of a failure analysis.</summary>
public enum FailureSeverity
{
    Informational,
    Warning,
    ActionRequired,
    Unknown
}

/// <summary>Confidence that bounded evidence supports the selected classification.</summary>
public enum FailureConfidence
{
    High,
    Medium,
    Low,
    Unknown
}

/// <summary>Aggregate state for the fixed, bounded local evidence projection.</summary>
public enum LogEvidenceOverallState
{
    Healthy,
    Degraded,
    Unavailable,
    Unknown
}

/// <summary>One already-redacted, bounded exception, event, or log evidence item.</summary>
public sealed record FailureEvidence(
    string Source,
    DateTimeOffset? AtUtc,
    string Summary,
    string? ExceptionType,
    IReadOnlyList<string> StackFrames,
    string? EventId);

/// <summary>Non-executing remediation guidance derived from bounded failure evidence.</summary>
public sealed record FailureRecommendation(
    string Code,
    string Label,
    string Summary);

/// <summary>Typed, bounded, non-mutating analysis for one opaque RMS service identifier.</summary>
public sealed record ServiceFailureAnalysis(
    string ServiceId,
    string ServiceDisplayName,
    FailureCategory Category,
    FailureSeverity Severity,
    FailureConfidence Confidence,
    string Summary,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<FailureEvidence> Evidence,
    IReadOnlyList<string> UnknownReasons,
    IReadOnlyList<FailureRecommendation> Recommendations);

/// <summary>
/// Safe snapshot of bounded diagnostic evidence for the server-owned RMS service catalog. The
/// service set is selected by the Agent; callers cannot submit service names, paths, or filters.
/// </summary>
public sealed record LogEvidenceSnapshot(
    DateTimeOffset CheckedAtUtc,
    LogEvidenceOverallState OverallState,
    IReadOnlyList<ServiceFailureAnalysis> Services);
