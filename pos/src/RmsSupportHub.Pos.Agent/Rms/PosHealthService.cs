using RmsSupportHub.Pos.Agent.Device;
using RmsSupportHub.Pos.Agent.Services;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using DomainDatabaseStatus = RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus;
using DomainDriftState = RmsSupportHub.Pos.Domain.Models.RmsComponentDriftState;
using DomainConsistencyState = RmsSupportHub.Pos.Domain.Models.RmsConsistencyState;

namespace RmsSupportHub.Pos.Agent.Rms;

/// <summary>
/// Composes the Slice A read-only health report. Every dependency is bounded and typed; a missing
/// or ambiguous observation remains Unknown instead of being presented as healthy.
/// </summary>
public sealed class PosHealthService(
    IRmsInstallationDiscovery discovery,
    IRmsDatabaseDiagnostics databases,
    RmsDatabaseHealthService databaseHealth,
    RmsConnectivityDiagnostics connectivity,
    ReadOnlyServiceStatusService services,
    IRmsDiagnosticEvidenceReader evidenceReader,
    TimeProvider timeProvider)
{
    public async Task<HealthReportDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var checkedAtUtc = timeProvider.GetUtcNow();
        RmsInstallationSnapshot installation;
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
            return new(
                HealthState.Unknown,
                "Installation evidence could not be read.",
                checkedAtUtc,
                [new("installation", HealthState.Unknown,
                    "Installation evidence could not be read.", checkedAtUtc,
                    "Inspect the fixed RMS installation files.")]);
        }

        var branchDatabase = databases.DiagnoseAsync(RmsDatabaseKind.Branch, cancellationToken);
        var cashierDatabase = databases.DiagnoseAsync(RmsDatabaseKind.Cashier, cancellationToken);
        var branchHealth = databaseHealth.GetAsync(RmsDatabaseKind.Branch, cancellationToken);
        var cashierHealth = databaseHealth.GetAsync(RmsDatabaseKind.Cashier, cancellationToken);
        var network = connectivity.GetAsync(installation, cancellationToken);
        var serviceStatuses = services.GetAsync(cancellationToken);
        await Task.WhenAll(branchDatabase, cashierDatabase, branchHealth, cashierHealth, network, serviceStatuses)
            .ConfigureAwait(false);

        var criticalEvidence = await Task.WhenAll(
            RmsServiceCatalog.Definitions.Select(definition =>
                ReadEvidenceSafeAsync(evidenceReader, definition.ServiceName, cancellationToken)))
            .ConfigureAwait(false);

        var checks = new List<HealthCheckDto>();
        checks.Add(new(
            "agent",
            HealthState.Healthy,
            "The local POS Agent responded to the authorized health request.",
            checkedAtUtc,
            null));
        checks.Add(new(
            "authorization",
            HealthState.Healthy,
            "Windows authentication and local administrator authorization were confirmed for this read.",
            checkedAtUtc,
            null));
        AddInstallationChecks(checks, installation, checkedAtUtc);
        AddEndpointCheck(checks, "main-server", "Main Server", network.Result.MainServer, checkedAtUtc);
        AddEndpointCheck(checks, "branch-server", "Branch Server", network.Result.BranchServer, checkedAtUtc);
        AddDatabaseCheck(checks, "branch-database", "Branch database", branchDatabase.Result, branchHealth.Result, checkedAtUtc);
        AddDatabaseCheck(checks, "cashier-database", "Cashier database", cashierDatabase.Result, cashierHealth.Result, checkedAtUtc);
        AddServiceChecks(checks, serviceStatuses.Result, checkedAtUtc);
        AddStorageChecks(checks, "branch", branchHealth.Result, checkedAtUtc);
        AddStorageChecks(checks, "cashier", cashierHealth.Result, checkedAtUtc);
        AddCriticalEvidenceCheck(checks, criticalEvidence, checkedAtUtc);

        var overall = Aggregate(checks.Select(check => check.State));
        var summary = overall switch
        {
            HealthState.Healthy => "POS health checks are healthy.",
            HealthState.Warning => "POS health checks found conditions to monitor.",
            HealthState.ActionRequired => "POS health checks found conditions requiring operator attention.",
            _ => "POS health evidence is incomplete."
        };
        return new(overall, summary, checkedAtUtc, checks);
    }

    private static void AddInstallationChecks(
        ICollection<HealthCheckDto> checks,
        RmsInstallationSnapshot installation,
        DateTimeOffset checkedAtUtc)
    {
        checks.Add(new(
            "installation",
            installation.InstallationDetected ? HealthState.Healthy : HealthState.Unknown,
            installation.InstallationDetected ? "RMS installation evidence is present." : "RMS installation evidence is unavailable.",
            checkedAtUtc,
            installation.InstallationDetected ? null : "Inspect the fixed RMS installation files."));

        var identityAvailable = !string.IsNullOrWhiteSpace(installation.BranchCode)
            && !string.IsNullOrWhiteSpace(installation.PosNumber)
            && !string.IsNullOrWhiteSpace(installation.ClientName);
        checks.Add(new(
            "identity",
            identityAvailable ? HealthState.Healthy : HealthState.Unknown,
            identityAvailable ? "Branch, POS, and client identity are available." : "Branch, POS, or client identity is unavailable.",
            checkedAtUtc,
            identityAvailable ? null : "Inspect RMS identity metadata."));

        var releaseState = string.IsNullOrWhiteSpace(installation.ProductRelease)
            ? HealthState.Unknown
            : HealthState.Healthy;
        checks.Add(new(
            "product-release",
            releaseState,
            releaseState == HealthState.Healthy ? "Product Release is available from the fixed release file." :
                "Product Release is unavailable from the fixed release file.",
            checkedAtUtc,
            releaseState == HealthState.Healthy ? null : "Inspect C:\\ProgramData\\RMS_Plus\\ReleaseNumber.txt."));

        var driftStates = installation.ComponentDrift ?? [];
        var driftState = driftStates.Any(item => item.State == DomainDriftState.Drifted)
            ? HealthState.ActionRequired
            : driftStates.Count == 0 || driftStates.Any(item => item.State == DomainDriftState.Unavailable)
                ? HealthState.Unknown
                : HealthState.Healthy;
        checks.Add(new(
            "release-drift",
            driftState,
            driftState switch
            {
                HealthState.Healthy => "Installed RMS component builds match Product Release.",
                HealthState.ActionRequired => "Installed RMS component builds differ from Product Release.",
                _ => "Component release-drift evidence is incomplete."
            },
            checkedAtUtc,
            driftState == HealthState.Healthy ? null : "Review component build evidence before maintenance."));

        var consistencyState = installation.Consistency.Warnings.Count > 0
            ? HealthState.ActionRequired
            : installation.Consistency.BranchCode == DomainConsistencyState.Unavailable
                ? HealthState.Unknown
                : HealthState.Healthy;
        checks.Add(new(
            "configuration-consistency",
            consistencyState,
            consistencyState switch
            {
                HealthState.Healthy => "Known RMS identity and version values are consistent.",
                HealthState.ActionRequired => "Known RMS configuration values are inconsistent.",
                _ => "Configuration consistency evidence is incomplete."
            },
            checkedAtUtc,
            consistencyState == HealthState.Healthy ? null : "Review the detailed consistency evidence."));
    }

    private static void AddEndpointCheck(
        ICollection<HealthCheckDto> checks,
        string code,
        string label,
        RmsEndpointDiagnosticDto endpoint,
        DateTimeOffset checkedAtUtc)
    {
        var state = !endpoint.Configured || endpoint.Reachability.Freshness == FreshnessState.Unknown
            ? HealthState.Unknown
            : endpoint.Reachability.Freshness == FreshnessState.Fresh ? HealthState.Healthy : HealthState.Warning;
        checks.Add(new(
            code,
            state,
            state switch
            {
                HealthState.Healthy => $"{label} endpoint is reachable.",
                HealthState.Warning => $"{label} endpoint is not reachable now.",
                _ => $"{label} endpoint evidence is unavailable."
            },
            endpoint.Reachability.LastCheckedUtc ?? checkedAtUtc,
            state == HealthState.Healthy ? null : $"Review the configured {label} endpoint."));
    }

    private static void AddDatabaseCheck(
        ICollection<HealthCheckDto> checks,
        string code,
        string label,
        RmsDatabaseDiagnosticResult result,
        RmsDatabaseHealthDto health,
        DateTimeOffset checkedAtUtc)
    {
        var state = result.Status switch
        {
            DomainDatabaseStatus.Reachable when result.DatabaseNameMatches == true => HealthState.Healthy,
            DomainDatabaseStatus.Reachable => HealthState.ActionRequired,
            DomainDatabaseStatus.NotConfigured or DomainDatabaseStatus.ConfigurationInvalid => HealthState.Unknown,
            _ => HealthState.ActionRequired
        };
        checks.Add(new(
            code,
            state,
            state switch
            {
                HealthState.Healthy => $"{label} is reachable and matches the expected database.",
                HealthState.Unknown => $"{label} configuration evidence is unavailable.",
                _ => $"{label} requires operator attention."
            },
            result.CheckedAtUtc == default ? checkedAtUtc : result.CheckedAtUtc,
            state == HealthState.Healthy ? null : "Review the detailed database evidence."));

        checks.Add(new(
            $"{code}-backups",
            health.Backups.State,
            health.Backups.Summary,
            checkedAtUtc,
            health.Backups.State == HealthState.Healthy ? null : "Create or review an approved database backup."));
    }

    private static void AddStorageChecks(
        ICollection<HealthCheckDto> checks,
        string database,
        RmsDatabaseHealthDto health,
        DateTimeOffset checkedAtUtc) =>
        checks.Add(new(
            $"{database}-storage",
            health.Storage.State,
            health.Storage.Summary,
            checkedAtUtc,
            health.Storage.State == HealthState.Healthy ? null : "Review local backup storage capacity."));

    private static void AddServiceChecks(
        ICollection<HealthCheckDto> checks,
        IReadOnlyList<ServiceSummaryDto> services,
        DateTimeOffset checkedAtUtc)
    {
        var expectedServiceIds = RmsServiceCatalog.Definitions
            .Select(definition => ServiceAllowList.ToServiceId(definition.ServiceName))
            .ToHashSet(StringComparer.Ordinal);
        var actualServiceIds = services
            .Select(service => service.ServiceId)
            .ToHashSet(StringComparer.Ordinal);
        var catalogMatches = expectedServiceIds.SetEquals(actualServiceIds);
        checks.Add(new(
            "service-catalog",
            catalogMatches ? HealthState.Healthy : HealthState.Unknown,
            catalogMatches
                ? "The canonical RMS service catalog is fixed to the three server-owned services."
                : "The canonical RMS service catalog could not be confirmed.",
            checkedAtUtc,
            catalogMatches ? null : "Inspect the server-owned service catalog before using controls."));

        foreach (var service in services)
        {
            var state = service.State switch
            {
                ServiceRuntimeState.Running => HealthState.Healthy,
                ServiceRuntimeState.Stopped or ServiceRuntimeState.Paused => HealthState.Warning,
                ServiceRuntimeState.NotFound => HealthState.ActionRequired,
                ServiceRuntimeState.Transitioning => HealthState.Warning,
                _ => HealthState.Unknown
            };
            checks.Add(new(
                $"service-{service.ServiceId}",
                state,
                $"{service.DisplayName}: {service.LastChecked.Detail}",
                service.LastChecked.LastCheckedUtc ?? checkedAtUtc,
                state == HealthState.Healthy ? null : "Use the typed service diagnostics and action controls."));
        }
    }

    private static void AddCriticalEvidenceCheck(
        ICollection<HealthCheckDto> checks,
        IReadOnlyList<RmsDiagnosticEvidenceReadResult> evidence,
        DateTimeOffset checkedAtUtc)
    {
        var criticalRecords = evidence
            .SelectMany(result => result.Records)
            .Where(record => record.Severity == RmsEvidenceSeverity.Error)
            .ToArray();
        var unknownReasons = evidence
            .SelectMany(result => result.UnknownReasons)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var state = criticalRecords.Length > 0
            ? HealthState.ActionRequired
            : unknownReasons.Length > 0 ? HealthState.Unknown : HealthState.Healthy;
        checks.Add(new(
            "recent-critical-errors",
            state,
            state switch
            {
                HealthState.ActionRequired => "Bounded recent critical-error evidence was found for an RMS service.",
                HealthState.Healthy => "No bounded recent critical-error evidence was found for the canonical RMS services.",
                _ => "Recent critical-error evidence could not be completely read."
            },
            checkedAtUtc,
            state == HealthState.Healthy ? null : "Review the bounded service failure evidence."));
    }

    private static async Task<RmsDiagnosticEvidenceReadResult> ReadEvidenceSafeAsync(
        IRmsDiagnosticEvidenceReader reader,
        string serviceName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadAsync(serviceName, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new([], ["Bounded recent critical-error evidence could not be read."]);
        }
    }

    private static HealthState Aggregate(IEnumerable<HealthState> states)
    {
        var values = states.ToArray();
        if (values.Contains(HealthState.ActionRequired)) return HealthState.ActionRequired;
        if (values.Contains(HealthState.Warning)) return HealthState.Warning;
        if (values.Contains(HealthState.Unknown)) return HealthState.Unknown;
        return HealthState.Healthy;
    }
}
