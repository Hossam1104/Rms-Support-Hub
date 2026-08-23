using System.Collections.Concurrent;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.Desktop.Wpf.Services;
using RmsSupportHub.Pos.Desktop.Wpf.ViewModels;

namespace RmsSupportHub.Pos.Desktop.Wpf.Tests;

public sealed class DashboardViewModelTests
{
    [Fact]
    public void StartsInLoadingStateWithSafeDefaults()
    {
        using var viewModel = new DashboardViewModel(new StubHealthClient());

        Assert.Equal(HealthViewState.Loading, viewModel.State);
        Assert.Equal("Loading", viewModel.StateLabel);
        Assert.Equal("Checking", viewModel.AgentStatus);
        Assert.Equal("No successful check yet", viewModel.LastSuccessfulCheckDisplay);
        Assert.True(viewModel.CanRefresh);
    }

    [Fact]
    public async Task SuccessfulHealthMovesToConnectedAndRecordsSafeValues()
    {
        var correlation = "correlation-connected";
        using var viewModel = new DashboardViewModel(new StubHealthClient(_ =>
            Task.FromResult(AgentHealthResult.Connected("Ready", "Ready", 1, false, correlation))));

        await viewModel.RefreshAsync();

        Assert.Equal(HealthViewState.Connected, viewModel.State);
        Assert.Equal("Connected", viewModel.StateLabel);
        Assert.Equal("Ready", viewModel.AgentStatus);
        Assert.Equal("Ready", viewModel.IpcStatus);
        Assert.Equal("v1", viewModel.ProtocolDisplay);
        Assert.Equal("No", viewModel.HubRequiredDisplay);
        Assert.Equal(correlation, viewModel.CorrelationId);
        Assert.NotNull(viewModel.LastSuccessfulCheck);
        Assert.Empty(viewModel.ErrorCode);
    }

    [Fact]
    public async Task ConnectedAgentLoadsHealthyFixedServiceSnapshot()
    {
        var serviceClient = new StubServiceHealthClient(_ =>
            Task.FromResult(ServiceHealthResult.Healthy(
                ServiceHealthViewState.Healthy,
                CreateRows(),
                DateTimeOffset.UtcNow,
                "service-correlation")));
        using var viewModel = new DashboardViewModel(
            new StubHealthClient(_ =>
                Task.FromResult(AgentHealthResult.Connected("Ready", "Ready", 1, false, "agent-correlation"))),
            serviceClient);

        await viewModel.RefreshAsync();

        Assert.Equal(HealthViewState.Connected, viewModel.State);
        Assert.Equal(ServiceHealthViewState.Healthy, viewModel.ServiceState);
        Assert.Equal(4, viewModel.ServiceItems.Count);
        Assert.Equal(4, viewModel.ServiceRunningCount);
        Assert.Equal(0, viewModel.ServiceStoppedCount);
        Assert.Equal("4 Running  |  0 Stopped  |  0 Unknown", viewModel.ServiceSummaryDisplay);
        Assert.Equal("agent-correlation", viewModel.CorrelationId);
        Assert.Equal(1, serviceClient.CallCount);
    }

    [Fact]
    public async Task StoppedRequiredServiceMovesWorkspaceToDegraded()
    {
        var serviceClient = new StubServiceHealthClient(_ =>
            Task.FromResult(ServiceHealthResult.Healthy(
                ServiceHealthViewState.Degraded,
                CreateRows(includeStopped: true),
                DateTimeOffset.UtcNow,
                "service-degraded")));
        using var viewModel = new DashboardViewModel(
            ConnectedAgentClient(),
            serviceClient);

        await viewModel.RefreshAsync();

        Assert.Equal(ServiceHealthViewState.Degraded, viewModel.ServiceState);
        Assert.Equal(3, viewModel.ServiceRunningCount);
        Assert.Equal(1, viewModel.ServiceStoppedCount);
        Assert.Contains("need attention", viewModel.ServiceStatusSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentUnavailableDoesNotProbeServicesAndKeepsSafeServiceState()
    {
        var serviceClient = new StubServiceHealthClient(_ =>
            throw new InvalidOperationException("The service client must not be called."));
        using var viewModel = new DashboardViewModel(
            new StubHealthClient(_ =>
                Task.FromResult(AgentHealthResult.Failure(
                    HealthViewState.Unavailable,
                    "agent_unavailable",
                    "RMS Support Agent is not available on this machine."))),
            serviceClient);

        await viewModel.RefreshAsync();

        Assert.Equal(HealthViewState.Unavailable, viewModel.State);
        Assert.Equal(ServiceHealthViewState.Unavailable, viewModel.ServiceState);
        Assert.Equal(0, serviceClient.CallCount);
        Assert.Empty(viewModel.ServiceItems);
    }

    [Fact]
    public async Task ServiceHealthFailureUsesBoundedSafeCopy()
    {
        var serviceClient = new StubServiceHealthClient(_ =>
            Task.FromResult(ServiceHealthResult.Failure(
                ServiceHealthViewState.TimedOut,
                "service_health_timeout",
                "Service health check timed out.",
                "service-timeout")));
        using var viewModel = new DashboardViewModel(ConnectedAgentClient(), serviceClient);

        await viewModel.RefreshAsync();

        Assert.Equal(ServiceHealthViewState.TimedOut, viewModel.ServiceState);
        Assert.Equal("Timed out", viewModel.ServiceStateLabel);
        Assert.Equal("service_health_timeout", viewModel.ServiceErrorCode);
        Assert.DoesNotContain("Exception", viewModel.ServiceErrorDetail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SID", viewModel.ServiceErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ServiceHealthViewState.ProtocolMismatch, "protocol_mismatch")]
    [InlineData(ServiceHealthViewState.SecurityVerificationFailed, "security_verification_failed")]
    [InlineData(ServiceHealthViewState.InvalidResponse, "invalid_response")]
    public async Task ServiceHealthFailureStatesRemainDistinctAndSafe(
        ServiceHealthViewState expectedState,
        string expectedCode)
    {
        var serviceClient = new StubServiceHealthClient(_ =>
            Task.FromResult(ServiceHealthResult.Failure(
                expectedState,
                expectedCode,
                "The bounded service-health failure is safe.")));
        using var viewModel = new DashboardViewModel(ConnectedAgentClient(), serviceClient);

        await viewModel.RefreshAsync();

        Assert.Equal(expectedState, viewModel.ServiceState);
        Assert.Equal(expectedCode, viewModel.ServiceErrorCode);
        Assert.DoesNotContain("Exception", viewModel.ServiceErrorDetail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SID", viewModel.ServiceErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ServiceRetryMovesFromUnavailableToHealthy()
    {
        var attempt = 0;
        var serviceClient = new StubServiceHealthClient(_ =>
        {
            attempt++;
            return Task.FromResult(attempt == 1
                ? ServiceHealthResult.Failure(
                    ServiceHealthViewState.Unavailable,
                    "agent_unavailable",
                    "RMS Support Agent is not available on this machine.")
                : ServiceHealthResult.Healthy(
                    ServiceHealthViewState.Healthy,
                    CreateRows(),
                    DateTimeOffset.UtcNow,
                    "service-retry"));
        });
        using var viewModel = new DashboardViewModel(ConnectedAgentClient(), serviceClient);

        await viewModel.RefreshAsync();
        Assert.Equal(ServiceHealthViewState.Unavailable, viewModel.ServiceState);

        await viewModel.RefreshAsync();

        Assert.Equal(ServiceHealthViewState.Healthy, viewModel.ServiceState);
        Assert.Equal(2, serviceClient.CallCount);
    }

    [Fact]
    public async Task ConcurrentRefreshesDoNotCreateParallelServiceHealthCalls()
    {
        var activeCalls = 0;
        var maximumActiveCalls = 0;
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serviceClient = new StubServiceHealthClient(async cancellationToken =>
        {
            var active = Interlocked.Increment(ref activeCalls);
            InterlockedExtensions.Max(ref maximumActiveCalls, active);
            await release.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref activeCalls);
            return ServiceHealthResult.Healthy(
                ServiceHealthViewState.Healthy,
                CreateRows(),
                DateTimeOffset.UtcNow,
                "service-concurrent");
        });
        using var viewModel = new DashboardViewModel(ConnectedAgentClient(), serviceClient);

        var first = viewModel.RefreshAsync();
        await serviceClient.FirstCallStarted.Task;
        var second = viewModel.RefreshAsync();
        release.SetResult(true);
        await Task.WhenAll(first, second);

        Assert.Equal(1, maximumActiveCalls);
        Assert.Equal(1, serviceClient.CallCount);
    }

    [Fact]
    public void ServicesCommandSwitchesToTheReadOnlyWorkspace()
    {
        using var viewModel = new DashboardViewModel(new StubHealthClient());

        viewModel.ShowServicesCommand.Execute(null);

        Assert.False(viewModel.IsDashboardVisible);
        Assert.True(viewModel.IsServicesVisible);

        viewModel.ShowDashboardCommand.Execute(null);

        Assert.True(viewModel.IsDashboardVisible);
        Assert.False(viewModel.IsServicesVisible);
    }

    [Fact]
    public async Task UnavailableKeepsRetryAvailableAndDoesNotLeakDetails()
    {
        using var viewModel = new DashboardViewModel(new StubHealthClient(_ =>
            Task.FromResult(AgentHealthResult.Failure(
                HealthViewState.Unavailable,
                "agent_unavailable",
                "RMS Support Agent is not available on this machine.",
                "correlation-unavailable"))));

        await viewModel.RefreshAsync();

        Assert.Equal(HealthViewState.Unavailable, viewModel.State);
        Assert.True(viewModel.CanRefresh);
        Assert.Equal("agent_unavailable", viewModel.ErrorCode);
        Assert.DoesNotContain("Exception", viewModel.ErrorDetail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SID", viewModel.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TimeoutMapsToTimedOutState()
    {
        using var viewModel = new DashboardViewModel(new StubHealthClient(_ =>
            Task.FromResult(AgentHealthResult.Failure(
                HealthViewState.TimedOut,
                "request_timeout",
                "The Agent did not respond before the health check timed out."))));

        await viewModel.RefreshAsync();

        Assert.Equal(HealthViewState.TimedOut, viewModel.State);
        Assert.Equal("Timed out", viewModel.StateLabel);
        Assert.True(viewModel.CanRefresh);
    }

    [Fact]
    public async Task ProtocolMismatchIsDistinctAndUsesSafeCopy()
    {
        using var viewModel = new DashboardViewModel(new StubHealthClient(_ =>
            Task.FromResult(AgentHealthResult.Failure(
                HealthViewState.ProtocolMismatch,
                "protocol_mismatch",
                "The desktop and Agent protocol versions are not compatible.",
                "correlation-version",
                2))));

        await viewModel.RefreshAsync();

        Assert.Equal(HealthViewState.ProtocolMismatch, viewModel.State);
        Assert.Equal("Version mismatch", viewModel.StateLabel);
        Assert.Equal("v2", viewModel.ProtocolDisplay);
        Assert.DoesNotContain("stack", viewModel.StatusSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidResponseIsDistinct()
    {
        using var viewModel = new DashboardViewModel(new StubHealthClient(_ =>
            Task.FromResult(AgentHealthResult.Failure(
                HealthViewState.InvalidResponse,
                "invalid_response",
                "The Agent returned an invalid health response."))));

        await viewModel.RefreshAsync();

        Assert.Equal(HealthViewState.InvalidResponse, viewModel.State);
        Assert.Equal("Invalid response", viewModel.StateLabel);
    }

    [Fact]
    public async Task SecurityVerificationFailureIsSafe()
    {
        using var viewModel = new DashboardViewModel(new StubHealthClient(_ =>
            Task.FromResult(AgentHealthResult.Failure(
                HealthViewState.SecurityVerificationFailed,
                "security_verification_failed",
                "The local Agent connection could not be verified."))));

        await viewModel.RefreshAsync();

        Assert.Equal(HealthViewState.SecurityVerificationFailed, viewModel.State);
        Assert.Equal("Connection not verified", viewModel.StateLabel);
        Assert.DoesNotContain("pipe", viewModel.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RetryCanMoveFromUnavailableToConnected()
    {
        var attempt = 0;
        using var viewModel = new DashboardViewModel(new StubHealthClient(_ =>
        {
            attempt++;
            return Task.FromResult(attempt == 1
                ? AgentHealthResult.Failure(
                    HealthViewState.Unavailable,
                    "agent_unavailable",
                    "RMS Support Agent is not available on this machine.")
                : AgentHealthResult.Connected("Ready", "Ready", 1, false, "correlation-retry"));
        }));

        await viewModel.RefreshAsync();
        Assert.Equal(HealthViewState.Unavailable, viewModel.State);

        await viewModel.RefreshAsync();

        Assert.Equal(HealthViewState.Connected, viewModel.State);
        Assert.Equal(2, attempt);
    }

    [Fact]
    public async Task ConcurrentRefreshesDoNotCreateParallelHealthCalls()
    {
        var activeCalls = 0;
        var maximumActiveCalls = 0;
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new StubHealthClient(async _ =>
        {
            var active = Interlocked.Increment(ref activeCalls);
            InterlockedExtensions.Max(ref maximumActiveCalls, active);
            await release.Task;
            Interlocked.Decrement(ref activeCalls);
            return AgentHealthResult.Connected("Ready", "Ready", 1, false, "correlation-concurrent");
        });
        using var viewModel = new DashboardViewModel(client);

        var first = viewModel.RefreshAsync();
        await client.FirstCallStarted.Task;
        var second = viewModel.RefreshAsync();
        release.SetResult(true);
        await Task.WhenAll(first, second);

        Assert.Equal(1, maximumActiveCalls);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task ShutdownCancellationDoesNotSurfaceAsAnApplicationError()
    {
        var client = new StubHealthClient(async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return AgentHealthResult.Connected("Ready", "Ready", 1, false, "never");
        });
        using var viewModel = new DashboardViewModel(client);

        var refresh = viewModel.RefreshAsync();
        await client.FirstCallStarted.Task;
        viewModel.Dispose();
        await refresh;

        Assert.Equal(HealthViewState.Loading, viewModel.State);
        Assert.DoesNotContain("exception", viewModel.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AutomaticRefreshIsBoundedAndCanOnlyStartOnce()
    {
        var client = new StubHealthClient(_ =>
            Task.FromResult(AgentHealthResult.Connected("Ready", "Ready", 1, false, "correlation-timer")));
        using var viewModel = new DashboardViewModel(client, TimeSpan.FromSeconds(5));

        viewModel.StartAutomaticRefresh();
        viewModel.StartAutomaticRefresh();

        Assert.True(viewModel.IsAutomaticRefreshRunning);
        Assert.Equal(TimeSpan.FromSeconds(5), viewModel.RefreshInterval);

        viewModel.Dispose();
        await Task.Delay(50);

        Assert.False(viewModel.IsAutomaticRefreshRunning);
        Assert.Equal(0, client.CallCount);
    }

    private sealed class StubHealthClient : ILocalAgentHealthClient
    {
        private readonly Func<CancellationToken, Task<AgentHealthResult>> responder;

        public StubHealthClient(Func<CancellationToken, Task<AgentHealthResult>>? responder = null)
        {
            this.responder = responder ?? (_ => Task.FromResult(AgentHealthResult.Failure(
                HealthViewState.Unavailable,
                "agent_unavailable",
                "RMS Support Agent is not available on this machine.")));
        }

        public int CallCount { get; private set; }

        public TaskCompletionSource<bool> FirstCallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<AgentHealthResult> GetHealthAsync(
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            FirstCallStarted.TrySetResult(true);
            return await responder(cancellationToken);
        }
    }

    private sealed class StubServiceHealthClient : ILocalServiceHealthClient
    {
        private readonly Func<CancellationToken, Task<ServiceHealthResult>> responder;

        public StubServiceHealthClient(Func<CancellationToken, Task<ServiceHealthResult>> responder)
        {
            this.responder = responder;
        }

        public int CallCount { get; private set; }

        public TaskCompletionSource<bool> FirstCallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ServiceHealthResult> GetHealthAsync(
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            FirstCallStarted.TrySetResult(true);
            return await responder(cancellationToken);
        }
    }

    private static ILocalAgentHealthClient ConnectedAgentClient() =>
        new StubHealthClient(_ =>
            Task.FromResult(AgentHealthResult.Connected("Ready", "Ready", 1, false, "agent-correlation")));

    private static IReadOnlyList<ServiceHealthRow> CreateRows(bool includeStopped = false) =>
    [
        new("RMS Branch Service", true, true,
            includeStopped ? ServiceHealthRowState.Stopped : ServiceHealthRowState.Running,
            includeStopped ? "stopped" : "running"),
        new("RMS Cashier Service", true, true, ServiceHealthRowState.Running, "running"),
        new("RMS Services Manager", true, true, ServiceHealthRowState.Running, "running"),
        new("RMS Support Agent", true, true, ServiceHealthRowState.Running, "running")
    ];

    private static class InterlockedExtensions
    {
        public static void Max(ref int location, int value)
        {
            while (true)
            {
                var current = Volatile.Read(ref location);
                if (current >= value || Interlocked.CompareExchange(ref location, value, current) == current)
                {
                    return;
                }
            }
        }
    }
}
