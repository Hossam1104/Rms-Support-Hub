using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Models;
using ContractOverallState = RmsSupportHub.Pos.Contracts.V1.Services.ServiceHealthOverallState;
using ContractRuntimeState = RmsSupportHub.Pos.Contracts.V1.Services.ServiceRuntimeState;
using DomainOverallState = RmsSupportHub.Pos.Domain.Models.ServiceHealthOverallState;

namespace RmsSupportHub.Pos.Agent.Services;

public static class ServiceHealthContractMapper
{
    public static ServiceHealthSnapshotDto Map(ServiceHealthSnapshot snapshot) => new(
        MapOverallState(snapshot.OverallState),
        snapshot.CheckedAtUtc,
        snapshot.Services.Select(MapItem).ToArray());

    public static IReadOnlyList<ServiceSummaryDto> MapLegacy(ServiceHealthSnapshot snapshot) =>
        snapshot.Services
            .Where(item => RmsServiceCatalog.Definitions.Any(definition =>
                string.Equals(definition.ServiceName, item.ServiceName, StringComparison.Ordinal)))
            .Select(item => MapLegacyItem(item, snapshot.CheckedAtUtc))
            .ToArray();

    private static ServiceHealthItemDto MapItem(ServiceHealthItem item) => new(
        item.ServiceId,
        item.DisplayName,
        item.Required,
        item.RuntimeState != ServiceStatus.NotFound,
        MapRuntimeState(item.RuntimeState),
        item.SafeStatusCode);

    private static ServiceSummaryDto MapLegacyItem(
        ServiceHealthItem item,
        DateTimeOffset checkedAtUtc)
    {
        var freshness = item.RuntimeState is ServiceStatus.Running
            or ServiceStatus.Stopped
            or ServiceStatus.Paused
            ? FreshnessState.Fresh
            : FreshnessState.Stale;

        return new(
            item.ServiceId,
            item.DisplayName,
            item.RuntimeState != ServiceStatus.NotFound,
            MapRuntimeState(item.RuntimeState),
            new(freshness, checkedAtUtc, ToSafeDetail(item.RuntimeState)),
            ReadOnlyServiceStatusService.AllowedActionsFor(item.RuntimeState),
            null);
    }

    private static ContractOverallState MapOverallState(DomainOverallState state) => state switch
    {
        DomainOverallState.Healthy => ContractOverallState.Healthy,
        DomainOverallState.Degraded => ContractOverallState.Degraded,
        _ => ContractOverallState.Unknown
    };

    private static ContractRuntimeState MapRuntimeState(ServiceStatus state) => state switch
    {
        ServiceStatus.Running => ContractRuntimeState.Running,
        ServiceStatus.Stopped => ContractRuntimeState.Stopped,
        ServiceStatus.Paused => ContractRuntimeState.Paused,
        ServiceStatus.Transitioning => ContractRuntimeState.Transitioning,
        ServiceStatus.NotFound => ContractRuntimeState.NotFound,
        _ => ContractRuntimeState.Unknown
    };

    private static string ToSafeDetail(ServiceStatus state) => state switch
    {
        ServiceStatus.Running => "Windows service is running.",
        ServiceStatus.Stopped => "Windows service is stopped.",
        ServiceStatus.Paused => "Windows service is paused.",
        ServiceStatus.Transitioning => "Windows service is changing state.",
        ServiceStatus.NotFound => "Configured Windows service was not found.",
        _ => "Windows service state is unavailable."
    };
}
