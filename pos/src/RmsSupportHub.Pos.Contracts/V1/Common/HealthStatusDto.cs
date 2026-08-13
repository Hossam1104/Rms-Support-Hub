namespace RmsSupportHub.Pos.Contracts.V1.Common;

/// <summary>
/// Minimal anonymous Agent health response. No machine, dependency, identity, or environment detail
/// is exposed.
/// </summary>
public sealed record HealthStatusDto(
    /// <summary>
    /// Identifies the foundation health check that produced this response, such as <c>live</c> or
    /// <c>ready</c>. It does not claim that downstream POS dependencies are usable.
    /// </summary>
    string Status);
