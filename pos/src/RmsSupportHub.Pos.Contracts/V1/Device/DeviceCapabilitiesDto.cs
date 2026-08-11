namespace RmsSupportHub.Pos.Contracts.V1.Device;

/// <summary>Safe capability metadata for this Agent. Browse roots expose display metadata only;
/// their host paths remain server-owned.</summary>
public sealed record DeviceCapabilitiesDto(
    string AgentVersion,
    string OperatingSystem,
    IReadOnlyList<BrowseRootDto> BrowseRoots);

public sealed record BrowseRootDto(string RootId, string DisplayName);
