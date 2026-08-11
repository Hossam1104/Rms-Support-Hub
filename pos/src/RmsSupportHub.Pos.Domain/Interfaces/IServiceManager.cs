using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Interfaces;

public interface IServiceManager
{
    Task<ServiceStatus> GetStatusAsync(string serviceName, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, ServiceStatus>> GetStatusesAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default);

    Task ControlAsync(string serviceName, ServiceControlAction action, CancellationToken cancellationToken = default);
}
