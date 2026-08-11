namespace RmsSupportHub.Pos.Contracts.V1.Common;

/// <summary>
/// Minimal anonymous Agent reachability response. No machine or environment detail is exposed.
/// </summary>
public sealed record HealthStatusDto(string Status);
