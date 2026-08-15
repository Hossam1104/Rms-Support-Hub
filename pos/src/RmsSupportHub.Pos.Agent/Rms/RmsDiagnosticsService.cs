using RmsSupportHub.Pos.Agent.Device;
using RmsSupportHub.Pos.Agent.Services;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using ContractConsistencyState = RmsSupportHub.Pos.Contracts.V1.Rms.RmsConsistencyState;
using ContractDatabaseStatus = RmsSupportHub.Pos.Contracts.V1.Rms.RmsDatabaseDiagnosticStatus;
using DomainConsistencyState = RmsSupportHub.Pos.Domain.Models.RmsConsistencyState;
using DomainDatabaseStatus = RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus;
using DomainDriftState = RmsSupportHub.Pos.Domain.Models.RmsComponentDriftState;
using ContractDriftState = RmsSupportHub.Pos.Contracts.V1.Rms.RmsComponentDriftState;

namespace RmsSupportHub.Pos.Agent.Rms;

/// <summary>
/// Composes the read-only RMS installation, database, endpoint, and canonical SCM diagnostics into
/// the single dashboard read model. No field in the returned contract is secret-bearing.
/// </summary>
public sealed class RmsDiagnosticsService(
    IRmsInstallationDiscovery discovery,
    IRmsDatabaseDiagnostics databases,
    RmsConnectivityDiagnostics connectivity,
    ReadOnlyServiceStatusService services,
    RmsDatabaseHealthService databaseHealth)
{
    public async Task<RmsDiagnosticsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var installation = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        var branchDatabase = databases.DiagnoseAsync(RmsDatabaseKind.Branch, cancellationToken);
        var cashierDatabase = databases.DiagnoseAsync(RmsDatabaseKind.Cashier, cancellationToken);
        var branchHealth = databaseHealth.GetAsync(RmsDatabaseKind.Branch, cancellationToken);
        var cashierHealth = databaseHealth.GetAsync(RmsDatabaseKind.Cashier, cancellationToken);
        var connectivityTask = connectivity.GetAsync(installation, cancellationToken);
        var servicesTask = services.GetAsync(cancellationToken);

        await Task.WhenAll(branchDatabase, cashierDatabase, branchHealth, cashierHealth, connectivityTask, servicesTask)
            .ConfigureAwait(false);

        return new(
            ToInstallationDto(installation),
            connectivityTask.Result,
            ToDatabaseDto(branchDatabase.Result, branchHealth.Result),
            ToDatabaseDto(cashierDatabase.Result, cashierHealth.Result),
            servicesTask.Result);
    }

    private static RmsInstallationDto ToInstallationDto(RmsInstallationSnapshot installation)
    {
        var mode = (installation.BranchInstalled, installation.CashierInstalled) switch
        {
            (true, true) => "Branch + Cashier",
            (true, false) => "Branch",
            (false, true) => "Cashier",
            _ => null
        };

        return new(
            installation.InstallationDetected,
            installation.BranchInstalled,
            installation.CashierInstalled,
            installation.BranchCode,
            installation.PosNumber,
            installation.InstallationGuid,
            installation.MainServerBranchId,
            installation.MainServerPosId,
            installation.MainServerUrl,
            installation.BranchServerAddress,
            mode,
            installation.ClientName,
            installation.ProductRelease,
            new(
                installation.Versions.BranchServerBuildNumber,
                installation.Versions.CashierServerBuildNumber,
                installation.Versions.CashierUiBuildNumber),
            new(
                MapConsistency(installation.Consistency.BranchCode),
                MapConsistency(installation.Consistency.PosIdentity),
                MapConsistency(installation.Consistency.MainServerBranchId),
                MapConsistency(installation.Consistency.MainServerPosId),
                MapConsistency(installation.Consistency.Version),
                installation.Consistency.Warnings),
            (installation.ComponentDrift ?? []).Select(drift => new RmsComponentDriftDto(
                drift.Component,
                drift.BuildNumber,
                drift.ProductRelease,
                drift.State switch
                {
                    DomainDriftState.Aligned => ContractDriftState.Aligned,
                    DomainDriftState.Drifted => ContractDriftState.Drifted,
                    _ => ContractDriftState.Unavailable
                },
                drift.Reason)).ToArray());
    }

    private static RmsDatabaseDiagnosticDto ToDatabaseDto(
        RmsDatabaseDiagnosticResult result,
        RmsDatabaseHealthDto health)
    {
        var freshness = result.Status switch
        {
            DomainDatabaseStatus.Reachable => FreshnessState.Fresh,
            DomainDatabaseStatus.NotConfigured
                or DomainDatabaseStatus.ConfigurationInvalid => FreshnessState.Unknown,
            _ => FreshnessState.Stale
        };

        return new(
            result.ExpectedDatabase,
            result.ConfiguredDatabase,
            result.ServerDisplay,
            result.Configured,
            result.DatabaseNameMatches,
            MapDatabaseStatus(result.Status),
            new EvidenceDto(freshness, result.CheckedAtUtc, result.Detail),
            health);
    }

    private static ContractConsistencyState MapConsistency(DomainConsistencyState state) => state switch
    {
        DomainConsistencyState.Consistent => ContractConsistencyState.Consistent,
        DomainConsistencyState.Mismatch => ContractConsistencyState.Mismatch,
        _ => ContractConsistencyState.Unavailable
    };

    private static ContractDatabaseStatus MapDatabaseStatus(DomainDatabaseStatus status) => status switch
    {
        DomainDatabaseStatus.NotConfigured => ContractDatabaseStatus.NotConfigured,
        DomainDatabaseStatus.ConfigurationInvalid => ContractDatabaseStatus.ConfigurationInvalid,
        DomainDatabaseStatus.DatabaseNameMismatch => ContractDatabaseStatus.DatabaseNameMismatch,
        DomainDatabaseStatus.Reachable => ContractDatabaseStatus.Reachable,
        DomainDatabaseStatus.AuthenticationFailed => ContractDatabaseStatus.AuthenticationFailed,
        DomainDatabaseStatus.DatabaseUnavailable => ContractDatabaseStatus.DatabaseUnavailable,
        _ => ContractDatabaseStatus.Unreachable
    };
}
