using RmsSupportHub.Pos.Agent.Device;
using RmsSupportHub.Pos.Agent.Invocation;
using RmsSupportHub.Pos.Agent.Services;
using RmsSupportHub.Pos.Application.Diagnostics;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Rms;

/// <summary>
/// Composes the read-only RMS installation, database, endpoint, and canonical SCM diagnostics into
/// the single dashboard read model. No field in the returned contract is secret-bearing.
/// </summary>
public sealed class RmsDiagnosticsService(
    RmsInstallationDiscoveryQueryHandler installationDiscovery,
    DatabaseHealthQueryHandler databaseHealthQuery,
    RmsConnectivityDiagnostics connectivity,
    ReadOnlyServiceStatusService services,
    RmsDatabaseHealthService databaseHealth)
{
    public async Task<RmsDiagnosticsDto> GetAsync(
        InvocationContext context,
        CancellationToken cancellationToken = default)
    {
        var installationResult = await installationDiscovery
            .HandleAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (!installationResult.Succeeded || installationResult.Value is null)
        {
            if (string.Equals(
                    installationResult.Error?.Code,
                    RmsInstallationDiscoveryFailureCodes.AuditUnavailable,
                    StringComparison.Ordinal))
            {
                throw new RmsInstallationDiscoveryAuditUnavailableException();
            }

            throw new InvalidOperationException(
                installationResult.Error?.Message ?? "The RMS installation discovery query failed.");
        }

        var installation = installationResult.Value;
        var databaseSnapshot = databaseHealthQuery.HandleAsync(context, cancellationToken);
        var branchHealth = databaseHealth.GetAsync(RmsDatabaseKind.Branch, cancellationToken);
        var cashierHealth = databaseHealth.GetAsync(RmsDatabaseKind.Cashier, cancellationToken);
        var connectivityTask = connectivity.GetAsync(installation, cancellationToken);
        var servicesTask = services.GetAsync(cancellationToken);

        await Task.WhenAll(databaseSnapshot, branchHealth, cashierHealth, connectivityTask, servicesTask)
            .ConfigureAwait(false);

        if (!databaseSnapshot.Result.Succeeded || databaseSnapshot.Result.Value is null)
        {
            throw new InvalidOperationException(
                databaseSnapshot.Result.Error?.Message
                ?? "The RMS database health query could not be completed.");
        }

        var branchDatabase = databaseSnapshot.Result.Value.Databases.Single(database =>
            database.DatabaseKind == RmsDatabaseKind.Branch);
        var cashierDatabase = databaseSnapshot.Result.Value.Databases.Single(database =>
            database.DatabaseKind == RmsDatabaseKind.Cashier);

        return new(
            RmsInstallationContractMapper.Map(installation),
            connectivityTask.Result,
            DatabaseHealthContractMapper.MapLegacy(branchDatabase, branchHealth.Result),
            DatabaseHealthContractMapper.MapLegacy(cashierDatabase, cashierHealth.Result),
            servicesTask.Result);
    }
}
