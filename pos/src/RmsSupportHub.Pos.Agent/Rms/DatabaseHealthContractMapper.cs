using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

using ContractOverallState = RmsSupportHub.Pos.Contracts.V1.Rms.RmsDatabaseHealthOverallState;
using ContractStatus = RmsSupportHub.Pos.Contracts.V1.Rms.RmsDatabaseDiagnosticStatus;
using DomainStatus = RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus;

namespace RmsSupportHub.Pos.Agent.Rms;

public static class DatabaseHealthContractMapper
{
    public static RmsDatabaseHealthSnapshotDto Map(DatabaseHealthSnapshot snapshot) => new(
        snapshot.CheckedAtUtc,
        MapOverallState(snapshot.OverallState),
        snapshot.Databases.Select(MapItem).ToArray());

    public static RmsDatabaseDiagnosticDto MapLegacy(
        DatabaseHealthItem item,
        RmsDatabaseHealthDto health) => new(
        item.ExpectedDatabase,
        item.ConfiguredDatabase,
        item.ServerDisplay,
        item.Configured,
        item.DatabaseNameMatches,
        MapStatus(item.Status),
        new(MapFreshness(item.Status), item.CheckedAtUtc, item.SafeDetail),
        health);

    private static RmsDatabaseHealthItemDto MapItem(DatabaseHealthItem item) => new(
        MapDatabaseKind(item.DatabaseKind),
        item.DisplayName,
        item.ExpectedDatabase,
        item.ConfiguredDatabase,
        item.ServerDisplay,
        item.Configured,
        item.DatabaseNameMatches,
        MapStatus(item.Status),
        item.SafeDetail,
        item.CheckedAtUtc);

    private static RmsDatabaseTarget MapDatabaseKind(RmsDatabaseKind database) => database switch
    {
        RmsDatabaseKind.Branch => RmsDatabaseTarget.Branch,
        RmsDatabaseKind.Cashier => RmsDatabaseTarget.Cashier,
        _ => throw new ArgumentOutOfRangeException(nameof(database), database, "Unknown RMS database.")
    };

    private static ContractOverallState MapOverallState(DatabaseHealthOverallState state) => state switch
    {
        DatabaseHealthOverallState.Healthy => ContractOverallState.Healthy,
        DatabaseHealthOverallState.Degraded => ContractOverallState.Degraded,
        DatabaseHealthOverallState.Unavailable => ContractOverallState.Unavailable,
        _ => ContractOverallState.Unknown
    };

    public static ContractStatus MapStatus(DomainStatus status) => status switch
    {
        DomainStatus.NotConfigured => ContractStatus.NotConfigured,
        DomainStatus.ConfigurationInvalid => ContractStatus.ConfigurationInvalid,
        DomainStatus.DatabaseNameMismatch => ContractStatus.DatabaseNameMismatch,
        DomainStatus.Reachable => ContractStatus.Reachable,
        DomainStatus.AuthenticationFailed => ContractStatus.AuthenticationFailed,
        DomainStatus.DatabaseUnavailable => ContractStatus.DatabaseUnavailable,
        DomainStatus.Unreachable => ContractStatus.Unreachable,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown RMS database status.")
    };

    private static FreshnessState MapFreshness(DomainStatus status) => status switch
    {
        DomainStatus.Reachable => FreshnessState.Fresh,
        DomainStatus.NotConfigured
            or DomainStatus.ConfigurationInvalid => FreshnessState.Unknown,
        _ => FreshnessState.Stale
    };
}
