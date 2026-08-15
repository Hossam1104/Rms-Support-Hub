namespace RmsSupportHub.Pos.Contracts.V1.Rms;

public sealed record RmsInstallationDto(
    bool Installed,
    bool BranchInstalled,
    bool CashierInstalled,
    string? BranchCode,
    string? PosNumber,
    string? InstallationGuid,
    string? MainServerBranchId,
    string? MainServerPosId,
    string? MainServerUrl,
    string? BranchServerAddress,
    string? InstallationMode,
    string? ClientName,
    string? ProductRelease,
    RmsVersionDto Versions,
    RmsConsistencyDto Consistency,
    IReadOnlyList<RmsComponentDriftDto> ComponentDrift);
