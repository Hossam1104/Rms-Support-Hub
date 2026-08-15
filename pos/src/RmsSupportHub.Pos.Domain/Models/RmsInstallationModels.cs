namespace RmsSupportHub.Pos.Domain.Models;

public enum RmsDatabaseKind
{
    Branch,
    Cashier
}

public enum RmsConnectionStringState
{
    Unavailable,
    Valid,
    Invalid
}

public enum RmsEndpointConfigurationState
{
    Unavailable,
    Configured,
    Invalid
}

public enum RmsConsistencyState
{
    Consistent,
    Mismatch,
    Unavailable
}

public enum RmsSqlProbeStatus
{
    Reachable,
    AuthenticationFailed,
    DatabaseUnavailable,
    Unreachable
}

public enum RmsComponentDriftState
{
    Aligned,
    Drifted,
    Unavailable
}

/// <summary>
/// Safe, non-serializable read model of the installed RMS+ suite. It contains only values selected
/// from the known RMS files. Raw connection strings and credentials are intentionally absent.
/// </summary>
public sealed record RmsInstallationSnapshot(
    bool InstallationDetected,
    bool BranchInstalled,
    bool CashierInstalled,
    string? BranchCode,
    string? PosNumber,
    string? MainServerBranchId,
    string? MainServerPosId,
    string? InstallationGuid,
    string? MainServerUrl,
    string? BranchServerAddress,
    string? ClientName,
    string? ProductRelease,
    RmsVersionSnapshot Versions,
    RmsConsistencySnapshot Consistency,
    RmsDatabaseConfiguration BranchDatabase,
    RmsDatabaseConfiguration CashierDatabase,
    RmsEndpointConfiguration MainServerEndpoint,
    RmsEndpointConfiguration BranchServerEndpoint,
    RmsServiceExpectationsSnapshot Services,
    IReadOnlyList<RmsComponentDriftSnapshot>? ComponentDrift = null);

public sealed record RmsVersionSnapshot(
    string? BranchServerBuildNumber,
    string? CashierServerBuildNumber,
    string? CashierUiBuildNumber);

public sealed record RmsDatabaseConfiguration(
    bool Configured,
    RmsConnectionStringState ConnectionStringState,
    string? DataSource,
    string? DatabaseName,
    bool? IntegratedSecurity);

public sealed record RmsEndpointConfiguration(
    RmsEndpointConfigurationState State,
    string? Host,
    int? Port,
    string? DisplayAddress)
{
    public bool Configured => State == RmsEndpointConfigurationState.Configured;
}

public sealed record RmsConsistencySnapshot(
    RmsConsistencyState BranchCode,
    RmsConsistencyState PosIdentity,
    RmsConsistencyState MainServerBranchId,
    RmsConsistencyState MainServerPosId,
    RmsConsistencyState Version,
    IReadOnlyList<string> Warnings);

public sealed record RmsServiceExpectationsSnapshot(
    bool ServicesManagerInstalled,
    bool ServicesManagerConfigurationDetected,
    bool BranchServiceConfigured,
    bool CashierServiceConfigured);

public sealed record RmsComponentDriftSnapshot(
    string Component,
    string? BuildNumber,
    string? ProductRelease,
    RmsComponentDriftState State,
    string Reason);

public sealed record RmsSqlProbeResult(
    RmsSqlProbeStatus Status,
    string? DatabaseName);
