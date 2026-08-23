using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;

internal static class ServiceHealthTestSupport
{
    public static ServiceHealthQueryHandler CreateHandler() =>
        new(new ServiceHealthReader(new FixedServiceManager(), TimeProvider.System));

    private sealed class FixedServiceManager : IServiceManager
    {
        public Task<ServiceStatus> GetStatusAsync(
            string serviceName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ServiceHealthCatalog.Definitions.Any(definition =>
                string.Equals(definition.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
                ? ServiceStatus.Running
                : ServiceStatus.Unknown);
        }

        public Task<IReadOnlyDictionary<string, ServiceStatus>> GetStatusesAsync(
            IEnumerable<string> serviceNames,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyDictionary<string, ServiceStatus>>(
                serviceNames.ToDictionary(
                    serviceName => serviceName,
                    serviceName => ServiceHealthCatalog.Definitions.Any(definition =>
                        string.Equals(definition.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
                        ? ServiceStatus.Running
                        : ServiceStatus.Unknown,
                    StringComparer.OrdinalIgnoreCase));
        }

        public Task ControlAsync(
            string serviceName,
            ServiceControlAction action,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The service-health IPC test must remain read-only.");
    }
}
