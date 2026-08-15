using RmsSupportHub.Pos.Contracts.V1.Device;
using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Agent.Device;

/// <summary>
/// Produces the bounded, server-owned diagnostics used by the first POS release. Connectivity
/// checks are TCP reachability evidence only; they do not expose connection strings, credentials,
/// host paths, or claims about application/database health.
/// </summary>
public sealed class DeviceDiagnosticsService(
    IRmsInstallationDiscovery discovery,
    RmsConnectivityDiagnostics connectivity)
{
    public async Task<DeviceIdentityDto> GetIdentityAsync(CancellationToken cancellationToken = default)
    {
        var installation = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        return new(
            installation.BranchCode ?? "Unavailable",
            installation.PosNumber ?? "Unavailable",
            installation.ProductRelease ?? "Unavailable",
            installation.ClientName ?? "RMS+");
    }

    public async Task<DeviceConnectivityDto> GetConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var installation = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        var localSql = await connectivity.ProbeLocalSqlAsync(
            installation.BranchDatabase,
            cancellationToken).ConfigureAwait(false);
        var rmsConnectivity = await connectivity.GetAsync(installation, cancellationToken).ConfigureAwait(false);
        return new(localSql, rmsConnectivity.MainServer.Reachability);
    }

    public DeviceCapabilitiesDto GetCapabilities()
    {
        var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        return new(version, Environment.OSVersion.VersionString, []);
    }

}
