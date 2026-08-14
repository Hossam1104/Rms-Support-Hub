using RmsSupportHub.Pos.Agent.Services;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class ReadOnlyServiceStatusServiceTests
{
    [Fact]
    public async Task CanonicalServiceRowsExposeAbsentRunningAndStoppedStates()
    {
        var manager = new FakeServiceManager(new Dictionary<string, ServiceStatus>(StringComparer.OrdinalIgnoreCase)
        {
            [RmsServiceCatalog.BranchServiceName] = ServiceStatus.NotFound,
            [RmsServiceCatalog.CashierServiceName] = ServiceStatus.Running,
            [RmsServiceCatalog.ServicesManagerServiceName] = ServiceStatus.Stopped
        });
        var service = new ReadOnlyServiceStatusService(
            new ServiceAllowList(),
            manager,
            TimeProvider.System);

        var rows = await service.GetAsync();

        Assert.Equal(3, rows.Count);
        Assert.Equal(
            ("RMS Branch Service", false, ServiceRuntimeState.NotFound),
            (rows[0].DisplayName, rows[0].Installed, rows[0].State));
        Assert.Empty(rows[0].AllowedActions);
        Assert.Equal(
            ("RMS Cashier Service", true, ServiceRuntimeState.Running),
            (rows[1].DisplayName, rows[1].Installed, rows[1].State));
        Assert.Equal(
            [ServiceActionKind.Stop, ServiceActionKind.Restart],
            rows[1].AllowedActions);
        Assert.Equal(
            ("RMS Services Manager", true, ServiceRuntimeState.Stopped),
            (rows[2].DisplayName, rows[2].Installed, rows[2].State));
        Assert.Equal(
            [ServiceActionKind.Start, ServiceActionKind.Restart],
            rows[2].AllowedActions);
    }

    private sealed class FakeServiceManager(
        IReadOnlyDictionary<string, ServiceStatus> statuses) : IServiceManager
    {
        public Task<ServiceStatus> GetStatusAsync(
            string serviceName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(statuses.GetValueOrDefault(serviceName, ServiceStatus.Unknown));
        }

        public Task<IReadOnlyDictionary<string, ServiceStatus>> GetStatusesAsync(
            IEnumerable<string> serviceNames,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyDictionary<string, ServiceStatus>>(
                serviceNames.ToDictionary(
                    serviceName => serviceName,
                    serviceName => statuses.GetValueOrDefault(serviceName, ServiceStatus.Unknown),
                    StringComparer.OrdinalIgnoreCase));
        }

        public Task ControlAsync(
            string serviceName,
            ServiceControlAction action,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Read-only test manager must not control services.");
    }
}
