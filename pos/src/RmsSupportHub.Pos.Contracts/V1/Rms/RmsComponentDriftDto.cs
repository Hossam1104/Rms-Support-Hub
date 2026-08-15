namespace RmsSupportHub.Pos.Contracts.V1.Rms;

/// <summary>Safe component-level release-drift evidence.</summary>
public sealed record RmsComponentDriftDto(
    /// <summary>Server-owned component label.</summary>
    string Component,
    /// <summary>Installed component build number, when readable.</summary>
    string? BuildNumber,
    /// <summary>Product release read from the fixed release file, when readable.</summary>
    string? ProductRelease,
    /// <summary>Comparison state.</summary>
    RmsComponentDriftState State,
    /// <summary>Safe reason for the comparison result.</summary>
    string Reason);
