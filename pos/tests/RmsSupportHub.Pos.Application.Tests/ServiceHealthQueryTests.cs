using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

public sealed class ServiceHealthQueryTests
{
    [Fact]
    public async Task LocalOperatorReceivesTheFixedRmsAndAgentCatalog()
    {
        var manager = new FakeServiceManager(ServiceStatus.Running);
        var handler = new ServiceHealthQueryHandler(
            new ServiceHealthReader(manager, TimeProvider.System));

        var result = await handler.HandleAsync(LocalOperatorContext("service-catalog"));

        Assert.True(result.Succeeded);
        var snapshot = Assert.IsType<ServiceHealthSnapshot>(result.Value);
        Assert.Equal(ServiceHealthOverallState.Healthy, snapshot.OverallState);
        Assert.Equal(ServiceHealthCatalog.Definitions.Count, snapshot.Services.Count);
        Assert.Equal(
            ServiceHealthCatalog.Definitions.Select(definition => definition.DisplayName),
            snapshot.Services.Select(service => service.DisplayName));
        Assert.All(snapshot.Services, service =>
        {
            Assert.True(ServiceIdentityCatalog.IsOpaqueServiceId(service.ServiceId));
            Assert.Equal("running", service.SafeStatusCode);
            Assert.DoesNotContain(service.ServiceName, service.DisplayName, StringComparison.Ordinal);
        });
        Assert.Equal(1, manager.StatusLookupCount);
        Assert.Equal(0, manager.ControlCallCount);
    }

    [Fact]
    public async Task RequiredStoppedServiceProducesDegradedSafeResult()
    {
        var statuses = ServiceHealthCatalog.Definitions.ToDictionary(
            definition => definition.ServiceName,
            _ => ServiceStatus.Running,
            StringComparer.OrdinalIgnoreCase);
        statuses[RmsServiceCatalog.CashierServiceName] = ServiceStatus.Stopped;
        var handler = CreateHandler(new FakeServiceManager(statuses));

        var result = await handler.HandleAsync(LocalOperatorContext("service-stopped"));

        Assert.True(result.Succeeded);
        var cashier = Assert.Single(result.Value!.Services, service =>
            service.ServiceName == RmsServiceCatalog.CashierServiceName);
        Assert.Equal(ServiceHealthOverallState.Degraded, result.Value.OverallState);
        Assert.Equal(ServiceStatus.Stopped, cashier.RuntimeState);
        Assert.Equal("stopped", cashier.SafeStatusCode);
    }

    [Fact]
    public async Task LocalAdministratorCanQueryTheSameReadOnlyProjection()
    {
        var handler = CreateHandler(new FakeServiceManager(ServiceStatus.Running));

        var result = await handler.HandleAsync(new InvocationContext(
            InvocationSource.LocalWpf,
            "local-admin",
            InvocationAuthorizationLevel.LocalAdministrator,
            "service-admin"));

        Assert.True(result.Succeeded);
        Assert.Equal(ServiceHealthOverallState.Healthy, result.Value!.OverallState);
    }

    [Fact]
    public async Task MissingServiceIsReportedAsNotInstalledWithoutNativeDetails()
    {
        var statuses = ServiceHealthCatalog.Definitions.ToDictionary(
            definition => definition.ServiceName,
            _ => ServiceStatus.Running,
            StringComparer.OrdinalIgnoreCase);
        statuses[RmsServiceCatalog.BranchServiceName] = ServiceStatus.NotFound;
        var handler = CreateHandler(new FakeServiceManager(statuses));

        var result = await handler.HandleAsync(LocalOperatorContext("service-missing"));

        Assert.True(result.Succeeded);
        var branch = Assert.Single(result.Value!.Services, service =>
            service.ServiceName == RmsServiceCatalog.BranchServiceName);
        Assert.Equal(ServiceHealthOverallState.Degraded, result.Value.OverallState);
        Assert.Equal("not_installed", branch.SafeStatusCode);
        Assert.DoesNotContain("Win32", branch.SafeStatusCode, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", branch.SafeStatusCode, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingDictionaryEntryProducesUnknownOverallState()
    {
        var handler = CreateHandler(new FakeServiceManager(
            new Dictionary<string, ServiceStatus>(StringComparer.OrdinalIgnoreCase)));

        var result = await handler.HandleAsync(LocalOperatorContext("service-unknown"));

        Assert.True(result.Succeeded);
        Assert.Equal(ServiceHealthOverallState.Unknown, result.Value!.OverallState);
        Assert.All(result.Value.Services, service =>
            Assert.Equal("unknown", service.SafeStatusCode));
    }

    [Fact]
    public void OptionalNonRunningServiceDoesNotDegradeTheOverallState()
    {
        var items = new ServiceHealthItem[]
        {
            new("required", "svc-required", "Required", true, ServiceStatus.Running, "running"),
            new("optional", "svc-optional", "Optional", false, ServiceStatus.Stopped, "stopped")
        };

        Assert.Equal(ServiceHealthOverallState.Healthy, ServiceHealthReader.DetermineOverallState(items));
        Assert.Equal(ServiceHealthOverallState.Unknown, ServiceHealthReader.DetermineOverallState([]));
    }

    [Fact]
    public async Task BoundedLookupTimeoutReturnsSafeFailure()
    {
        var manager = new FakeServiceManager(ServiceStatus.Running, delay: Timeout.InfiniteTimeSpan);
        var handler = new ServiceHealthQueryHandler(
            new ServiceHealthReader(manager, TimeProvider.System, TimeSpan.FromMilliseconds(20)));

        var result = await handler.HandleAsync(LocalOperatorContext("service-timeout"));

        Assert.False(result.Succeeded);
        Assert.Equal("service_health_timeout", result.Error?.Code);
        Assert.Equal("Service health check timed out.", result.Error?.Message);
    }

    [Fact]
    public async Task UnexpectedLookupFailureReturnsSafeFailure()
    {
        var handler = CreateHandler(new FakeServiceManager(
            ServiceStatus.Running,
            exception: new InvalidOperationException("native service details must not cross the boundary")));

        var result = await handler.HandleAsync(LocalOperatorContext("service-failure"));

        Assert.False(result.Succeeded);
        Assert.Equal("service_health_unavailable", result.Error?.Code);
        Assert.DoesNotContain("native", result.Error?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingOrRemoteContextIsDeniedBeforeServiceLookup()
    {
        var manager = new FakeServiceManager(ServiceStatus.Running);
        var handler = CreateHandler(manager);

        var missing = await handler.HandleAsync(null);
        var unauthenticated = await handler.HandleAsync(new InvocationContext(
            InvocationSource.LocalWpf,
            "unauthenticated",
            InvocationAuthorizationLevel.Unauthenticated,
            "service-unauthenticated"));
        var remote = await handler.HandleAsync(new InvocationContext(
            InvocationSource.RemoteHub,
            "remote-admin",
            InvocationAuthorizationLevel.RemoteAdministrator,
            "service-remote"));
        var invalid = await handler.HandleAsync(new InvocationContext(
            (InvocationSource)999,
            "invalid-source",
            InvocationAuthorizationLevel.LocalAdministrator,
            "service-invalid"));

        Assert.False(missing.Succeeded);
        Assert.Equal("invocation_context_missing", missing.Error?.Code);
        Assert.False(unauthenticated.Succeeded);
        Assert.Equal("diagnostic_authorization_required", unauthenticated.Error?.Code);
        Assert.False(remote.Succeeded);
        Assert.Equal("diagnostic_authorization_required", remote.Error?.Code);
        Assert.False(invalid.Succeeded);
        Assert.Equal("diagnostic_authorization_required", invalid.Error?.Code);
        Assert.Equal(0, manager.StatusLookupCount);
    }

    private static ServiceHealthQueryHandler CreateHandler(IServiceManager manager) =>
        new(new ServiceHealthReader(manager, TimeProvider.System));

    private static InvocationContext LocalOperatorContext(string correlationId) => new(
        InvocationSource.LocalWpf,
        "local-operator",
        InvocationAuthorizationLevel.LocalOperator,
        correlationId);

    private sealed class FakeServiceManager : IServiceManager
    {
        private readonly IReadOnlyDictionary<string, ServiceStatus> statuses;
        private readonly TimeSpan? delay;
        private readonly Exception? exception;

        public FakeServiceManager(
            ServiceStatus status,
            TimeSpan? delay = null,
            Exception? exception = null)
            : this(
                ServiceHealthCatalog.Definitions.ToDictionary(
                    definition => definition.ServiceName,
                    _ => status,
                    StringComparer.OrdinalIgnoreCase),
                delay,
                exception)
        {
        }

        public FakeServiceManager(
            IReadOnlyDictionary<string, ServiceStatus> statuses,
            TimeSpan? delay = null,
            Exception? exception = null)
        {
            this.statuses = statuses;
            this.delay = delay;
            this.exception = exception;
        }

        public int StatusLookupCount { get; private set; }

        public int ControlCallCount { get; private set; }

        public Task<ServiceStatus> GetStatusAsync(
            string serviceName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(statuses.GetValueOrDefault(serviceName, ServiceStatus.Unknown));

        public async Task<IReadOnlyDictionary<string, ServiceStatus>> GetStatusesAsync(
            IEnumerable<string> serviceNames,
            CancellationToken cancellationToken = default)
        {
            StatusLookupCount++;
            if (delay is { } wait)
            {
                await Task.Delay(wait, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (exception is not null)
            {
                throw exception;
            }

            return serviceNames.ToDictionary(
                serviceName => serviceName,
                serviceName => statuses.GetValueOrDefault(serviceName, ServiceStatus.Unknown),
                StringComparer.OrdinalIgnoreCase);
        }

        public Task ControlAsync(
            string serviceName,
            ServiceControlAction action,
            CancellationToken cancellationToken = default)
        {
            ControlCallCount++;
            throw new InvalidOperationException("Service health is read-only.");
        }
    }
}
