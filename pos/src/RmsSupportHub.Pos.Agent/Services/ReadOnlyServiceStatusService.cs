using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Agent.Services;

/// <summary>
/// Reads the service allow-list owned by the Agent and projects current Windows service state
/// plus only the typed actions valid for the observed state. Authorization remains enforced by the
/// protected action endpoint and server-side runtime gates.
/// </summary>
public sealed class ReadOnlyServiceStatusService(
    ServiceAllowList allowList,
    IServiceManager manager,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<ServiceSummaryDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        var services = await allowList.GetAsync(cancellationToken).ConfigureAwait(false);
        var serviceNames = services.Select(service => service.ServiceName).ToArray();

        var statuses = await manager.GetStatusesAsync(serviceNames, cancellationToken).ConfigureAwait(false);
        var checkedAt = clock.GetUtcNow();

        return services
            .Select(service => ToDto(
                service,
                statuses.GetValueOrDefault(service.ServiceName, ServiceStatus.Unknown),
                checkedAt))
            .ToArray();
    }

    internal static string ToServiceId(string serviceName) => ServiceAllowList.ToServiceId(serviceName);

    private static ServiceSummaryDto ToDto(
        AllowListedService service,
        ServiceStatus status,
        DateTimeOffset checkedAt)
    {
        var (state, detail, freshness) = status switch
        {
            ServiceStatus.Running => (ServiceRuntimeState.Running, "Windows service is running.", FreshnessState.Fresh),
            ServiceStatus.Stopped => (ServiceRuntimeState.Stopped, "Windows service is stopped.", FreshnessState.Fresh),
            ServiceStatus.NotFound => (ServiceRuntimeState.NotFound, "Configured Windows service was not found.", FreshnessState.Stale),
            _ => (ServiceRuntimeState.Unknown, "Windows service state is unavailable.", FreshnessState.Stale)
        };

        return new(
            service.ServiceId,
            service.ServiceName,
            state,
            new EvidenceDto(freshness, checkedAt, detail),
            AllowedActionsFor(status),
            null);
    }

    internal static IReadOnlyList<ServiceActionKind> AllowedActionsFor(ServiceStatus status) =>
        status switch
        {
            ServiceStatus.Running => [ServiceActionKind.Stop, ServiceActionKind.Restart],
            ServiceStatus.Stopped => [ServiceActionKind.Start, ServiceActionKind.Restart],
            _ => []
        };
}
