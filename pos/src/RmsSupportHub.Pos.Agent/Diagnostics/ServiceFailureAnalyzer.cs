using RmsSupportHub.Pos.Agent.Services;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using DomainDriftState = RmsSupportHub.Pos.Domain.Models.RmsComponentDriftState;

namespace RmsSupportHub.Pos.Agent.Diagnostics;

/// <summary>
/// Correlates current allow-listed SCM state with bounded, redacted local evidence. It only
/// classifies and recommends; it never starts a process, invokes a command, or changes state.
/// </summary>
public sealed class ServiceFailureAnalyzer(
    ServiceAllowList allowList,
    IServiceManager serviceManager,
    IRmsDiagnosticEvidenceReader evidenceReader,
    IRmsInstallationDiscovery discovery,
    TimeProvider timeProvider)
{
    public async Task<ServiceFailureAnalysisDto?> AnalyzeAsync(
        string? serviceId,
        CancellationToken cancellationToken = default)
    {
        var target = await allowList.ResolveAsync(serviceId, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return null;
        }

        var checkedAtUtc = timeProvider.GetUtcNow();
        var unknownReasons = new List<string>();
        var evidence = new RmsDiagnosticEvidenceReadResult([], []);
        try
        {
            evidence = await evidenceReader.ReadAsync(target.ServiceName, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            unknownReasons.Add("Bounded local evidence could not be read.");
        }

        unknownReasons.AddRange(evidence.UnknownReasons.Take(8));
        var status = ServiceStatus.Unknown;
        try
        {
            status = await serviceManager.GetStatusAsync(target.ServiceName, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            unknownReasons.Add("Windows service state could not be read.");
        }

        RmsInstallationSnapshot? installation = null;
        try
        {
            installation = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            unknownReasons.Add("Installed component drift evidence could not be read.");
        }

        var drift = FindDrift(target.ServiceName, installation);
        var records = evidence.Records.Take(12).Select(ToContract).ToArray();
        var classification = Classify(status, records, drift, unknownReasons.Count > 0);
        var recommendations = BuildRecommendations(classification.Category, status, drift, records);
        var summary = BuildSummary(classification.Category, classification.Severity, status, records, drift, unknownReasons.Count > 0);

        return new(
            target.ServiceId,
            target.DisplayName,
            classification.Category,
            classification.Severity,
            classification.Confidence,
            summary,
            checkedAtUtc,
            records,
            unknownReasons.Distinct(StringComparer.Ordinal).Take(8).ToArray(),
            recommendations);
    }

    private static (FailureCategory Category, FailureSeverity Severity, FailureConfidence Confidence) Classify(
        ServiceStatus status,
        IReadOnlyList<FailureEvidenceDto> records,
        RmsComponentDriftState? drift,
        bool evidenceIncomplete)
    {
        if (drift == DomainDriftState.Drifted)
        {
            return (FailureCategory.VersionDrift, FailureSeverity.ActionRequired, FailureConfidence.High);
        }

        if (status == ServiceStatus.NotFound)
        {
            return (FailureCategory.ServiceStartFailure, FailureSeverity.ActionRequired, FailureConfidence.High);
        }

        if (status is ServiceStatus.Stopped or ServiceStatus.Paused)
        {
            return (FailureCategory.ServiceStopped, FailureSeverity.Warning, FailureConfidence.High);
        }

        if (records.Count > 0)
        {
            var text = string.Join(' ', records.Select(record => record.Summary));
            var category = text.Contains("sql", StringComparison.OrdinalIgnoreCase)
                || text.Contains("database", StringComparison.OrdinalIgnoreCase)
                ? FailureCategory.Database
                : text.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("network", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("connect", StringComparison.OrdinalIgnoreCase)
                    ? FailureCategory.Network
                    : FailureCategory.Crash;
            return (category, FailureSeverity.ActionRequired, FailureConfidence.Medium);
        }

        if (status == ServiceStatus.Running && !evidenceIncomplete && drift is not DomainDriftState.Unavailable)
        {
            return (FailureCategory.None, FailureSeverity.Informational, FailureConfidence.Low);
        }

        return (FailureCategory.Unknown, FailureSeverity.Unknown, FailureConfidence.Unknown);
    }

    private static IReadOnlyList<FailureRecommendationDto> BuildRecommendations(
        FailureCategory category,
        ServiceStatus status,
        RmsComponentDriftState? drift,
        IReadOnlyList<FailureEvidenceDto> records)
    {
        var recommendations = new List<FailureRecommendationDto>();
        if (category == FailureCategory.ServiceStopped || status == ServiceStatus.Paused)
        {
            recommendations.Add(new("start-service", "Review Start", "The service is not running; confirm the local state before using the typed Start action."));
        }

        if (category is FailureCategory.Crash or FailureCategory.ServiceStartFailure)
        {
            recommendations.Add(new("review-evidence", "Review bounded evidence", "Inspect the redacted exception, stack, event, and service evidence before retrying."));
        }

        if (category == FailureCategory.VersionDrift || drift == DomainDriftState.Drifted)
        {
            recommendations.Add(new("review-drift", "Review release drift", "Installed component build evidence differs from Product Release."));
        }

        if (category == FailureCategory.Database)
        {
            recommendations.Add(new("review-database", "Review database health", "Review typed database reachability and approved-backup evidence."));
        }

        if (records.Count > 0 || category != FailureCategory.None)
        {
            recommendations.Add(new("generate-bundle", "Generate Support Bundle", "Capture the current redacted evidence for escalation."));
        }

        return recommendations.Take(4).ToArray();
    }

    private static string BuildSummary(
        FailureCategory category,
        FailureSeverity severity,
        ServiceStatus status,
        IReadOnlyList<FailureEvidenceDto> records,
        RmsComponentDriftState? drift,
        bool incomplete) =>
        category switch
        {
            FailureCategory.None => "No bounded failure evidence was found while the service was running.",
            FailureCategory.ServiceStopped => "The allow-listed Windows service is stopped.",
            FailureCategory.ServiceStartFailure => "The allow-listed Windows service is missing or cannot be confirmed by SCM.",
            FailureCategory.VersionDrift => "The installed component build differs from Product Release.",
            FailureCategory.Database => "Bounded evidence suggests a database-related service failure.",
            FailureCategory.Network => "Bounded evidence suggests a network or endpoint-related service failure.",
            FailureCategory.Crash => "Bounded exception, event, or log evidence suggests a service failure.",
            _ when status == ServiceStatus.Running && incomplete => "The service is running, but diagnostic evidence is incomplete.",
            _ => "The service failure state could not be established from bounded evidence."
        };

    private static FailureEvidenceDto ToContract(RmsDiagnosticEvidenceRecord record) => new(
        record.Source,
        record.AtUtc,
        record.Summary,
        record.ExceptionType,
        record.StackFrames,
        record.EventId);

    private static RmsComponentDriftState? FindDrift(
        string serviceName,
        RmsInstallationSnapshot? installation)
    {
        if (installation?.ComponentDrift is not { } drift) return null;
        var component = serviceName switch
        {
            RmsServiceCatalog.BranchServiceName => "Branch Server",
            RmsServiceCatalog.CashierServiceName => "Cashier Server",
            _ => null
        };
        return component is null
            ? null
            : drift.FirstOrDefault(item => string.Equals(item.Component, component, StringComparison.OrdinalIgnoreCase))?.State;
    }
}
