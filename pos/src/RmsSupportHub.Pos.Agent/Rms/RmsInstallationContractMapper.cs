using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Models;
using ContractConsistencyState = RmsSupportHub.Pos.Contracts.V1.Rms.RmsConsistencyState;
using ContractDriftState = RmsSupportHub.Pos.Contracts.V1.Rms.RmsComponentDriftState;
using DomainConsistencyState = RmsSupportHub.Pos.Domain.Models.RmsConsistencyState;
using DomainDriftState = RmsSupportHub.Pos.Domain.Models.RmsComponentDriftState;

namespace RmsSupportHub.Pos.Agent.Rms;

public static class RmsInstallationContractMapper
{
    public static RmsInstallationDto Map(RmsInstallationSnapshot installation)
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

    private static ContractConsistencyState MapConsistency(DomainConsistencyState state) => state switch
    {
        DomainConsistencyState.Consistent => ContractConsistencyState.Consistent,
        DomainConsistencyState.Mismatch => ContractConsistencyState.Mismatch,
        _ => ContractConsistencyState.Unavailable
    };
}
