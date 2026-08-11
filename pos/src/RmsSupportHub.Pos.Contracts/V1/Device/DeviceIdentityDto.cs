namespace RmsSupportHub.Pos.Contracts.V1.Device;

/// <summary>Non-secret device/branch identity shown on the overview and device screens.</summary>
public sealed record DeviceIdentityDto(
    string BranchCode,
    string PosNumber,
    string Release,
    string ClientName);
