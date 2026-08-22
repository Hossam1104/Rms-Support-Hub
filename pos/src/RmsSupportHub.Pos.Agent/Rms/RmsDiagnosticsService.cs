using RmsSupportHub.Pos.Agent.Device;
using RmsSupportHub.Pos.Agent.Invocation;
using RmsSupportHub.Pos.Agent.Services;
using RmsSupportHub.Pos.Application.Diagnostics;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using ContractDatabaseStatus = RmsSupportHub.Pos.Contracts.V1.Rms.RmsDatabaseDiagnosticStatus;
using DomainDatabaseStatus = RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus;

namespace RmsSupportHub.Pos.Agent.Rms;

/// <summary>
/// Composes the read-only RMS installation, database, endpoint, and canonical SCM diagnostics into
/// the single dashboard read model. No field in the returned contract is secret-bearing.
/// </summary>
public sealed class RmsDiagnosticsService(
    RmsInstallationDiscoveryQueryHandler installationDiscovery,
    IRmsDatabaseDiagnostics databases,
    RmsConnectivityDiagnostics connectivity,
    ReadOnlyServiceStatusService services,
    RmsDatabaseHealthService databaseHealth)
{
    private static readonly InvocationContext InternalContext = new(
        InvocationSource.LegacyLoopbackHttp,
        "agent-service",
        InvocationAuthorizationLevel.LocalAdministrator,
        "agent-internal");

    public Task<RmsDiagnosticsDto> GetAsync(CancellationToken cancellationToken = default) =>
        GetAsync(InternalContext, cancellationToken);

    public async Task<RmsDiagnosticsDto> GetAsync(
        InvocationContext context,
        CancellationToken cancellationToken = default)
    {
        var installationResult = await installationDiscovery
            .HandleAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (!installationResult.Succeeded || installationResult.Value is null)
        {
            throw new InvalidOperationException(
                installationResult.Error?.Message ?? "The RMS installation discovery query failed.");
        }

        var installation = installationResult.Value;
        var branchDatabase = databases.DiagnoseAsync(RmsDatabaseKind.Branch, cancellationToken);
        var cashierDatabase = databases.DiagnoseAsync(RmsDatabaseKind.Cashier, cancellationToken);
        var branchHealth = databaseHealth.GetAsync(RmsDatabaseKind.Branch, cancellationToken);
        var cashierHealth = databaseHealth.GetAsync(RmsDatabaseKind.Cashier, cancellationToken);
        var connectivityTask = connectivity.GetAsync(installation, cancellationToken);
        var servicesTask = services.GetAsync(cancellationToken);

        await Task.WhenAll(branchDatabase, cashierDatabase, branchHealth, cashierHealth, connectivityTask, servicesTask)
            .ConfigureAwait(false);

        return new(
            RmsInstallationContractMapper.Map(installation),
            connectivityTask.Result,
            ToDatabaseDto(branchDatabase.Result, branchHealth.Result),
            ToDatabaseDto(cashierDatabase.Result, cashierHealth.Result),
            servicesTask.Result);
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
