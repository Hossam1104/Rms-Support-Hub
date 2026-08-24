namespace RmsSupportHub.Pos.Contracts.V1.Services;

public enum ServiceHealthOverallState
{
    Healthy,
    Degraded,
    Unknown
}

/// <summary>Typed read-only service-health projection returned over Local IPC.</summary>
public sealed record ServiceHealthSnapshotDto(
    ServiceHealthOverallState OverallState,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<ServiceHealthItemDto> Services);

/// <summary>
/// One fixed service row. The raw Windows service name and native status/error details are never
/// part of this contract.
/// </summary>
public sealed record ServiceHealthItemDto(
    string ServiceId,
    string DisplayName,
    bool Required,
    bool Installed,
    ServiceRuntimeState RuntimeState,
    string SafeStatusCode,
    bool CanControl = false);
