using RmsSupportHub.Pos.Application.Diagnostics;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;

using ApplicationCategory = RmsSupportHub.Pos.Application.Diagnostics.FailureCategory;
using ApplicationConfidence = RmsSupportHub.Pos.Application.Diagnostics.FailureConfidence;
using ApplicationSeverity = RmsSupportHub.Pos.Application.Diagnostics.FailureSeverity;
using ApplicationOverallState = RmsSupportHub.Pos.Application.Diagnostics.LogEvidenceOverallState;

namespace RmsSupportHub.Pos.Agent.Diagnostics;

/// <summary>
/// Maps the transport-neutral Application diagnostic projection to the existing V1 contract at the
/// Agent composition boundary. Classification, redaction, and bounds remain in their existing
/// Application/Domain/Infrastructure seams.
/// </summary>
public static class ServiceFailureContractMapper
{
    public static ServiceFailureAnalysisDto Map(ServiceFailureAnalysis analysis) => new(
        analysis.ServiceId,
        analysis.ServiceDisplayName,
        MapCategory(analysis.Category),
        MapSeverity(analysis.Severity),
        MapConfidence(analysis.Confidence),
        analysis.Summary,
        analysis.CheckedAtUtc,
        analysis.Evidence.Select(Map).ToArray(),
        analysis.UnknownReasons,
        analysis.Recommendations.Select(Map).ToArray());

    public static LogEvidenceSnapshotDto Map(LogEvidenceSnapshot snapshot) => new(
        snapshot.CheckedAtUtc,
        MapOverallState(snapshot.OverallState),
        snapshot.Services.Select(Map).ToArray());

    private static FailureEvidenceDto Map(FailureEvidence evidence) => new(
        evidence.Source,
        evidence.AtUtc,
        evidence.Summary,
        evidence.ExceptionType,
        evidence.StackFrames,
        evidence.EventId);

    private static FailureRecommendationDto Map(FailureRecommendation recommendation) => new(
        recommendation.Code,
        recommendation.Label,
        recommendation.Summary);

    private static RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureCategory MapCategory(ApplicationCategory category) => category switch
    {
        ApplicationCategory.None => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureCategory.None,
        ApplicationCategory.ServiceStopped => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureCategory.ServiceStopped,
        ApplicationCategory.ServiceStartFailure => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureCategory.ServiceStartFailure,
        ApplicationCategory.Crash => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureCategory.Crash,
        ApplicationCategory.Database => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureCategory.Database,
        ApplicationCategory.Network => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureCategory.Network,
        ApplicationCategory.Configuration => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureCategory.Configuration,
        ApplicationCategory.VersionDrift => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureCategory.VersionDrift,
        _ => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureCategory.Unknown
    };

    private static RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureSeverity MapSeverity(ApplicationSeverity severity) => severity switch
    {
        ApplicationSeverity.Informational => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureSeverity.Informational,
        ApplicationSeverity.Warning => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureSeverity.Warning,
        ApplicationSeverity.ActionRequired => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureSeverity.ActionRequired,
        _ => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureSeverity.Unknown
    };

    private static RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureConfidence MapConfidence(ApplicationConfidence confidence) => confidence switch
    {
        ApplicationConfidence.High => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureConfidence.High,
        ApplicationConfidence.Medium => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureConfidence.Medium,
        ApplicationConfidence.Low => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureConfidence.Low,
        _ => RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureConfidence.Unknown
    };

    private static RmsSupportHub.Pos.Contracts.V1.Diagnostics.LogEvidenceOverallState MapOverallState(ApplicationOverallState state) => state switch
    {
        ApplicationOverallState.Healthy => RmsSupportHub.Pos.Contracts.V1.Diagnostics.LogEvidenceOverallState.Healthy,
        ApplicationOverallState.Degraded => RmsSupportHub.Pos.Contracts.V1.Diagnostics.LogEvidenceOverallState.Degraded,
        ApplicationOverallState.Unavailable => RmsSupportHub.Pos.Contracts.V1.Diagnostics.LogEvidenceOverallState.Unavailable,
        _ => RmsSupportHub.Pos.Contracts.V1.Diagnostics.LogEvidenceOverallState.Unknown
    };
}
