using System.Net.Sockets;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Device;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Device;

/// <summary>
/// Produces the bounded, server-owned diagnostics used by the first POS release. Connectivity
/// checks are TCP reachability evidence only; they do not expose connection strings, credentials,
/// host paths, or claims about application/database health.
/// </summary>
public sealed class DeviceDiagnosticsService(
    IAgentConfigurationStore configurations,
    TimeProvider clock)
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    public async Task<DeviceIdentityDto> GetIdentityAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await configurations.LoadAsync(cancellationToken).ConfigureAwait(false);
        return new(
            configuration.BranchCode,
            configuration.PosNumber,
            configuration.Release,
            configuration.ClientName);
    }

    public async Task<DeviceConnectivityDto> GetConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await configurations.LoadAsync(cancellationToken).ConfigureAwait(false);
        var localSql = await ProbeSqlAsync(configuration.SqlInstance, cancellationToken).ConfigureAwait(false);
        var mainServer = await ProbeHttpEndpointAsync(configuration.ApiBaseUrl, cancellationToken).ConfigureAwait(false);
        return new(localSql, mainServer);
    }

    public DeviceCapabilitiesDto GetCapabilities()
    {
        var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        return new(version, Environment.OSVersion.VersionString, []);
    }

    private async Task<EvidenceDto> ProbeSqlAsync(string? sqlInstance, CancellationToken cancellationToken)
    {
        if (!TryParseSqlEndpoint(sqlInstance, out var host, out var port))
        {
            return new(FreshnessState.Unknown, null, "SQL endpoint is not configured for a reachability check.");
        }

        return await ProbeAsync(
            host,
            port,
            "SQL endpoint is reachable; database health was not queried.",
            "SQL endpoint is unreachable.",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<EvidenceDto> ProbeHttpEndpointAsync(string? address, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host)
            || (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return new(FreshnessState.Unknown, null, "Main-server address is not configured for a reachability check.");
        }

        var port = uri.IsDefaultPort
            ? uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80
            : uri.Port;
        return await ProbeAsync(
            uri.Host,
            port,
            "Main-server TCP endpoint is reachable; application health was not queried.",
            "Main-server TCP endpoint is unreachable.",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<EvidenceDto> ProbeAsync(
        string host,
        int port,
        string successDetail,
        string failureDetail,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ProbeTimeout);
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            return new(FreshnessState.Fresh, clock.GetUtcNow(), successDetail);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new(FreshnessState.Stale, clock.GetUtcNow(), failureDetail);
        }
    }

    private static bool TryParseSqlEndpoint(string? value, out string host, out int port)
    {
        host = string.Empty;
        port = 1433;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (candidate.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[4..];
        }

        var separator = candidate.LastIndexOf(',');
        if (separator > 0)
        {
            if (!int.TryParse(candidate[(separator + 1)..], out port) || port is < 1 or > 65535)
            {
                return false;
            }

            candidate = candidate[..separator];
        }

        var instanceSeparator = candidate.IndexOf('\\');
        if (instanceSeparator >= 0)
        {
            candidate = candidate[..instanceSeparator];
        }

        candidate = candidate.Trim().Trim('[', ']');
        if (candidate is "." or "(local)")
        {
            candidate = "localhost";
        }

        if (candidate.Length == 0
            || candidate.Any(char.IsWhiteSpace)
            || candidate.Any(char.IsControl))
        {
            return false;
        }

        host = candidate;
        return true;
    }
}
