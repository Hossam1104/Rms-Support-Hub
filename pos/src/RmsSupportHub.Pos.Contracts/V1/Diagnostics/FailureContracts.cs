using RmsSupportHub.Pos.Contracts.V1.Common;

namespace RmsSupportHub.Pos.Contracts.V1.Diagnostics;

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

/// <summary>Confidence that the bounded evidence supports the selected classification.</summary>
public enum FailureConfidence
{
    High,
    Medium,
    Low,
    Unknown
}

/// <summary>One redacted exception, event, or bounded log evidence item.</summary>
public sealed record FailureEvidenceDto(
    /// <summary>Safe evidence source label such as SCM, Application Event Log, or RMS log.</summary>
    string Source,
    /// <summary>UTC evidence time, when the source supplied one.</summary>
    DateTimeOffset? AtUtc,
    /// <summary>Redacted bounded evidence summary.</summary>
    string Summary,
    /// <summary>Exception type only, without a message or assembly path.</summary>
    string? ExceptionType,
    /// <summary>Redacted bounded stack-frame labels.</summary>
    IReadOnlyList<string> StackFrames,
    /// <summary>Safe provider event identifier, when available.</summary>
    string? EventId);

/// <summary>Non-executing remediation guidance derived from the failure evidence.</summary>
public sealed record FailureRecommendationDto(
    /// <summary>Stable recommendation code.</summary>
    string Code,
    /// <summary>Operator-facing recommendation label.</summary>
    string Label,
    /// <summary>Safe reason for the recommendation.</summary>
    string Summary);

/// <summary>Typed, bounded, non-mutating analysis for one opaque RMS service identifier.</summary>
public sealed record ServiceFailureAnalysisDto(
    /// <summary>Opaque server-owned service identifier.</summary>
    string ServiceId,
    /// <summary>Safe service display name.</summary>
    string ServiceDisplayName,
    /// <summary>Selected failure category.</summary>
    FailureCategory Category,
    /// <summary>Conservative failure severity.</summary>
    FailureSeverity Severity,
    /// <summary>Evidence confidence.</summary>
    FailureConfidence Confidence,
    /// <summary>Safe operator summary.</summary>
    string Summary,
    /// <summary>UTC analysis time.</summary>
    DateTimeOffset CheckedAtUtc,
    /// <summary>Bounded redacted evidence items.</summary>
    IReadOnlyList<FailureEvidenceDto> Evidence,
    /// <summary>Safe reasons why evidence could not be established.</summary>
    IReadOnlyList<string> UnknownReasons,
    /// <summary>Non-executing next-step guidance.</summary>
    IReadOnlyList<FailureRecommendationDto> Recommendations);
