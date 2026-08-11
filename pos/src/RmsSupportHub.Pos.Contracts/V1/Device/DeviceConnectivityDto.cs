using RmsSupportHub.Pos.Contracts.V1.Common;

namespace RmsSupportHub.Pos.Contracts.V1.Device;

/// <summary>
/// Local SQL and main-server reachability, kept as two independent evidence nodes because they
/// have different remedies and must never be conflated (plan section 7.3).
/// </summary>
public sealed record DeviceConnectivityDto(
    EvidenceDto LocalSql,
    EvidenceDto MainServer);
