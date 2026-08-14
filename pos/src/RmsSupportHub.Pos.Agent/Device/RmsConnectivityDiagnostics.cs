using System.Net.Sockets;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Device;

/// <summary>
/// Performs bounded TCP reachability probes for endpoints discovered from the installed RMS
/// configuration. It deliberately makes no application-health claim.
/// </summary>
public sealed class RmsConnectivityDiagnostics(TimeProvider clock)
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    public async Task<RmsConnectivityDto> GetAsync(
        RmsInstallationSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var mainServer = await ProbeAsync(
            snapshot.MainServerEndpoint,
            "Main-server TCP endpoint is reachable; application health was not queried.",
            "Main-server TCP endpoint is unreachable.",
            cancellationToken).ConfigureAwait(false);
        var branchServer = await ProbeAsync(
            snapshot.BranchServerEndpoint,
            "Branch-server TCP endpoint is reachable; application health was not queried.",
            "Branch-server TCP endpoint is unreachable.",
            cancellationToken).ConfigureAwait(false);

        return new(
            ToEndpointDto(snapshot.MainServerEndpoint, mainServer),
            ToEndpointDto(snapshot.BranchServerEndpoint, branchServer));
    }

    public Task<EvidenceDto> ProbeLocalSqlAsync(
        RmsDatabaseConfiguration database,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseSqlEndpoint(database.DataSource, out var host, out var port))
        {
            return Task.FromResult(new EvidenceDto(
                FreshnessState.Unknown,
                null,
                "The installed RMS SQL endpoint is not configured for a reachability check."));
        }

        return ProbeHostAsync(
            host,
            port,
            "SQL endpoint is reachable; database health was not queried.",
            "SQL endpoint is unreachable.",
            cancellationToken);
    }

    private async Task<EvidenceDto> ProbeAsync(
        RmsEndpointConfiguration endpoint,
        string successDetail,
        string failureDetail,
        CancellationToken cancellationToken)
    {
        if (!endpoint.Configured || endpoint.Host is null || endpoint.Port is null)
        {
            return new(
                FreshnessState.Unknown,
                null,
                endpoint.State == RmsEndpointConfigurationState.Invalid
                    ? "The installed RMS endpoint configuration is invalid."
                    : "The installed RMS endpoint is not configured for a reachability check.");
        }

        return await ProbeHostAsync(
            endpoint.Host,
            endpoint.Port.Value,
            successDetail,
            failureDetail,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<EvidenceDto> ProbeHostAsync(
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

    private static RmsEndpointDiagnosticDto ToEndpointDto(
        RmsEndpointConfiguration endpoint,
        EvidenceDto evidence) =>
        new(endpoint.Configured, endpoint.DisplayAddress, evidence);

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
