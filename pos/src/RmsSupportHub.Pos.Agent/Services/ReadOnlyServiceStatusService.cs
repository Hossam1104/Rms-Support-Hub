using System.Security.Cryptography;
using System.Text;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Agent.Services;

/// <summary>
/// Reads the service allow-list owned by the Agent and projects current Windows service state.
/// The first release deliberately returns no allowed actions, so a read-only response cannot be
/// mistaken for authorization to start, stop, or restart a service.
/// </summary>
public sealed class ReadOnlyServiceStatusService(
    IAgentConfigurationStore configurations,
    IServiceManager manager,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<ServiceSummaryDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await configurations.LoadAsync(cancellationToken).ConfigureAwait(false);
        var serviceNames = configuration.Services
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var statuses = await manager.GetStatusesAsync(serviceNames, cancellationToken).ConfigureAwait(false);
        var checkedAt = clock.GetUtcNow();

        return serviceNames
            .Select(name => ToDto(name, statuses.GetValueOrDefault(name, ServiceStatus.Unknown), checkedAt))
            .ToArray();
    }

    internal static string ToServiceId(string serviceName) =>
        "svc-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serviceName.Trim())))
            .ToLowerInvariant()[..16];

    private static ServiceSummaryDto ToDto(string serviceName, ServiceStatus status, DateTimeOffset checkedAt)
    {
        var (state, detail, freshness) = status switch
        {
            ServiceStatus.Running => (ServiceRuntimeState.Running, "Windows service is running.", FreshnessState.Fresh),
            ServiceStatus.Stopped => (ServiceRuntimeState.Stopped, "Windows service is stopped.", FreshnessState.Fresh),
            ServiceStatus.NotFound => (ServiceRuntimeState.NotFound, "Configured Windows service was not found.", FreshnessState.Stale),
            _ => (ServiceRuntimeState.Unknown, "Windows service state is unavailable.", FreshnessState.Stale)
        };

        return new(
            ToServiceId(serviceName),
            serviceName,
            state,
            new EvidenceDto(freshness, checkedAt, detail),
            [],
            null);
    }
}
