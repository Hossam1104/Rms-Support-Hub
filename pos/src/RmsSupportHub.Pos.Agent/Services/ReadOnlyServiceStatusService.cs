using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Services;

/// <summary>
/// Reads the service allow-list owned by the Agent and projects current Windows service state
/// plus only the typed actions valid for the observed state. Authorization remains enforced by the
/// protected action endpoint and server-side runtime gates.
/// </summary>
public sealed class ReadOnlyServiceStatusService(ServiceHealthReader reader)
{
    public async Task<IReadOnlyList<ServiceSummaryDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await reader.GetAsync(cancellationToken).ConfigureAwait(false);
        return ServiceHealthContractMapper.MapLegacy(snapshot);
    }

    internal static string ToServiceId(string serviceName) => ServiceIdentityCatalog.ToServiceId(serviceName);

    internal static IReadOnlyList<ServiceActionKind> AllowedActionsFor(ServiceStatus status) =>
        status switch
        {
            ServiceStatus.Running => [ServiceActionKind.Stop, ServiceActionKind.Restart],
            ServiceStatus.Stopped => [ServiceActionKind.Start, ServiceActionKind.Restart],
            _ => []
        };
}
