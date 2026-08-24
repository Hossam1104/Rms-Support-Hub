using System.Collections.Concurrent;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Exceptions;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

public sealed class ServiceControlApplicationServiceTests
{
    private ServiceMutationAuthorizationStore? authorizationStore;

    [Fact]
    public async Task FixedCatalogAndAuthorizationRejectUnsafeTargetsBeforeManagerDispatch()
    {
        var manager = new FakeServiceManager(ServiceStatus.Stopped);
        var service = Create(manager);
        var serviceId = ServiceIdentityCatalog.ToServiceId(RmsServiceCatalog.CashierServiceName);

        var raw = await service.ExecuteAsync(Request("RMS.CashierService", ServiceControlAction.Start, null, "raw"));
        var agent = await service.ExecuteAsync(Request(
            ServiceIdentityCatalog.ToServiceId(AgentProductIdentity.PermanentServiceName),
            ServiceControlAction.Start,
            null,
            "agent"));
        var unknown = await service.ExecuteAsync(Request("svc-0000000000000000", ServiceControlAction.Start, null, "unknown"));
        var operatorResult = await service.ExecuteAsync(Request(serviceId, ServiceControlAction.Start, null, "operator", level: InvocationAuthorizationLevel.LocalOperator));
        var remote = await service.ExecuteAsync(Request(serviceId, ServiceControlAction.Start, null, "remote", source: InvocationSource.RemoteHub, level: InvocationAuthorizationLevel.RemoteAdministrator));
        var unauthenticated = await service.ExecuteAsync(Request(serviceId, ServiceControlAction.Start, null, "unauth", level: InvocationAuthorizationLevel.Unauthenticated));

        Assert.Equal("service_control_not_allowed", raw.Code);
        Assert.Equal("agent_self_control_not_allowed", agent.Code);
        Assert.Equal("service_not_found", unknown.Code);
        Assert.Equal("administrator_authorization_required", operatorResult.Code);
        Assert.Equal("administrator_authorization_required", remote.Code);
        Assert.Equal("administrator_authorization_required", unauthenticated.Code);
        Assert.Empty(manager.Calls);
    }

    [Fact]
    public async Task StartHasOrderedDurableAuditAndVerifiesRunningState()
    {
        var manager = new FakeServiceManager(ServiceStatus.Stopped);
        var audit = new RecordingAuditSink();
        var service = Create(manager, audit);
        var result = await service.ExecuteAsync(Request(
            ServiceIdentityCatalog.ToServiceId(RmsServiceCatalog.CashierServiceName),
            ServiceControlAction.Start,
            null,
            "start-1"));

        Assert.Equal(ServiceControlOperationState.Completed, result.State);
        Assert.Equal("completed", result.Code);
        Assert.Equal(ServiceStatus.Running, result.ObservedState);
        Assert.Single(manager.Calls);
        Assert.Equal(ServiceControlAction.Start, manager.Calls.Single().Action);
        Assert.Equal(
            new[] { "requested", "accepted", "started", "dispatch", "completed" },
            audit.Events.Select(item => item.Outcome));
    }

    [Fact]
    public async Task CompletedIdempotentRetryRecoversWithoutReissuingMutationAuthorization()
    {
        var manager = new FakeServiceManager(ServiceStatus.Stopped);
        var service = Create(manager);
        var firstRequest = Request(
            ServiceIdentityCatalog.ToServiceId(RmsServiceCatalog.CashierServiceName),
            ServiceControlAction.Start,
            null,
            "idempotent-retry");

        var first = await service.ExecuteAsync(firstRequest);
        var repeated = await service.ExecuteAsync(firstRequest with { MutationAuthorization = string.Empty });

        Assert.Equal(ServiceControlOperationState.Completed, first.State);
        Assert.Equal(first, repeated);
        Assert.Single(manager.Calls);
    }

    [Fact]
    public async Task StopAndRestartRequireExactConfirmationAndRestartObservesBothPhases()
    {
        var manager = new FakeServiceManager(ServiceStatus.Running);
        var service = Create(manager);
        var serviceId = ServiceIdentityCatalog.ToServiceId(RmsServiceCatalog.CashierServiceName);

        var missing = await service.ExecuteAsync(Request(serviceId, ServiceControlAction.Stop, null, "stop-missing"));
        var wrong = await service.ExecuteAsync(Request(serviceId, ServiceControlAction.Restart, "STOP", "restart-wrong"));
        Assert.Equal("confirmation_required", missing.Code);
        Assert.Equal("confirmation_required", wrong.Code);
        Assert.Empty(manager.Calls);

        var stop = await service.ExecuteAsync(Request(serviceId, ServiceControlAction.Stop, "STOP", "stop-ok"));
        Assert.Equal(ServiceControlOperationState.Completed, stop.State);
        Assert.Equal(ServiceStatus.Stopped, stop.ObservedState);

        var restart = await service.ExecuteAsync(Request(serviceId, ServiceControlAction.Restart, "RESTART", "restart-ok"));
        Assert.Equal(ServiceControlOperationState.Completed, restart.State);
        Assert.Equal(ServiceStatus.Running, restart.ObservedState);
        Assert.Equal(
            new[] { ServiceControlAction.Stop, ServiceControlAction.Stop, ServiceControlAction.Start },
            manager.Calls.Select(item => item.Action));
    }

    [Fact]
    public async Task FinalAuditFailureNeverReportsSuccess()
    {
        var manager = new FakeServiceManager(ServiceStatus.Stopped);
        var audit = new RecordingAuditSink { FailOutcome = "completed" };
        var service = Create(manager, audit);
        var result = await service.ExecuteAsync(Request(
            ServiceIdentityCatalog.ToServiceId(RmsServiceCatalog.CashierServiceName),
            ServiceControlAction.Start,
            null,
            "audit-final"));

        Assert.Equal(ServiceControlOperationState.OutcomeUnknown, result.State);
        Assert.Equal("audit_unavailable", result.Code);
        Assert.True(result.RecoveryRequired);
        Assert.Equal(ServiceStatus.Running, result.ObservedState);
        Assert.Single(manager.Calls);
    }

    [Fact]
    public async Task AuditUnavailableBeforeDispatchFailsClosed()
    {
        var manager = new FakeServiceManager(ServiceStatus.Stopped);
        var audit = new RecordingAuditSink { FailOutcome = "requested" };
        var service = Create(manager, audit);
        var result = await service.ExecuteAsync(Request(
            ServiceIdentityCatalog.ToServiceId(RmsServiceCatalog.CashierServiceName),
            ServiceControlAction.Start,
            null,
            "audit-pre"));

        Assert.Equal("audit_unavailable", result.Code);
        Assert.Empty(manager.Calls);
    }

    [Fact]
    public async Task CancellationBeforeDispatchDoesNotCallManager()
    {
        var manager = new FakeServiceManager(ServiceStatus.Stopped);
        var service = Create(manager);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await service.ExecuteAsync(
            Request(
                ServiceIdentityCatalog.ToServiceId(RmsServiceCatalog.CashierServiceName),
                ServiceControlAction.Start,
                null,
                "cancel-before"),
            cancellation.Token);

        Assert.Equal(ServiceControlOperationState.Cancelled, result.State);
        Assert.Empty(manager.Calls);
    }

    [Fact]
    public void MutationAuthorizationIsOneUseAndBoundToEveryMutationFact()
    {
        var options = new ServiceControlOptions();
        var store = new ServiceMutationAuthorizationStore(options, TimeProvider.System);
        var serviceId = ServiceIdentityCatalog.ToServiceId(RmsServiceCatalog.CashierServiceName);
        var issue = store.Issue("S-1", serviceId, ServiceControlAction.Stop, "STOP", "corr-1");

        var mismatch = store.TryConsume(issue.Token, "S-1", serviceId, ServiceControlAction.Restart, "RESTART", "corr-1");
        var replay = store.TryConsume(issue.Token, "S-1", serviceId, ServiceControlAction.Stop, "STOP", "corr-1");

        Assert.True(issue.Succeeded);
        Assert.False(mismatch.Succeeded);
        Assert.Equal(ServiceMutationAuthorizationFailure.Mismatch, mismatch.Failure);
        Assert.Equal(ServiceMutationAuthorizationFailure.Unknown, replay.Failure);

        var second = store.Issue("S-1", serviceId, ServiceControlAction.Stop, "STOP", "corr-2");
        var wrongPrincipal = store.TryConsume(second.Token, "S-2", serviceId, ServiceControlAction.Stop, "STOP", "corr-2");
        Assert.Equal(ServiceMutationAuthorizationFailure.Mismatch, wrongPrincipal.Failure);
    }

    [Fact]
    public async Task MissingMutationAuthorizationFailsClosedBeforeManagerDispatch()
    {
        var manager = new FakeServiceManager(ServiceStatus.Stopped);
        var service = Create(manager);
        var serviceId = ServiceIdentityCatalog.ToServiceId(RmsServiceCatalog.CashierServiceName);
        var request = new ServiceControlRequest(
            new(InvocationSource.LocalWpf, "S-1-5-21-1000", InvocationAuthorizationLevel.LocalAdministrator, "corr-missing-token"),
            serviceId,
            ServiceControlAction.Start,
            null,
            "missing-token",
            string.Empty);

        var result = await service.ExecuteAsync(request);

        Assert.Equal("mutation_authorization_invalid", result.Code);
        Assert.Empty(manager.Calls);
    }

    [Fact]
    public async Task SameTargetConflictsDifferentTargetsCanRunAndCoordinatorStaysBounded()
    {
        var clock = new ManualTimeProvider();
        var options = new ServiceControlOptions
        {
            MaxIdempotencyEntries = 32,
            IdempotencyRetention = TimeSpan.FromSeconds(1),
            PollInterval = TimeSpan.FromMilliseconds(1),
            TransitionTimeout = TimeSpan.FromSeconds(1),
            OverallOperationTimeout = TimeSpan.FromSeconds(2)
        };
        var coordinator = new ServiceMutationCoordinator(options, clock);
        var cashier = ServiceIdentityCatalog.ToServiceId(RmsServiceCatalog.CashierServiceName);
        var branch = ServiceIdentityCatalog.ToServiceId(RmsServiceCatalog.BranchServiceName);
        var first = coordinator.TryBegin("S-1", cashier, ServiceControlAction.Start, "", "one", "c1");
        Assert.Equal(ServiceControlReservationState.Reserved, first.State);
        var sameTarget = coordinator.TryBegin("S-1", cashier, ServiceControlAction.Stop, "STOP", "two", "c2");
        Assert.Equal(ServiceControlReservationState.Busy, sameTarget.State);
        var differentTarget = coordinator.TryBegin("S-1", branch, ServiceControlAction.Start, "", "three", "c3");
        Assert.Equal(ServiceControlReservationState.Reserved, differentTarget.State);
        first.Lease!.Dispose();
        differentTarget.Lease!.Dispose();

        for (var index = 0; index < 2_000; index++)
        {
            var reservation = coordinator.TryBegin("S-1", cashier, ServiceControlAction.Start, "", $"key-{index}", $"corr-{index}");
            Assert.Equal(ServiceControlReservationState.Reserved, reservation.State);
            coordinator.Complete("S-1", cashier, $"key-{index}", new(
                Guid.NewGuid().ToString("N"),
                cashier,
                ServiceControlAction.Start,
                ServiceControlOperationState.Completed,
                100,
                "verified",
                ServiceStatus.Running,
                "completed",
                "done",
                $"corr-{index}"));
            reservation.Lease!.Dispose();
            clock.Advance(TimeSpan.FromSeconds(2));
        }

        Assert.InRange(coordinator.RetainedEntryCount, 0, 32);
        Assert.Equal(0, coordinator.ActiveTargetCount);
    }

    private ServiceControlApplicationService Create(
        FakeServiceManager manager,
        RecordingAuditSink? audit = null)
    {
        var options = new ServiceControlOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(1),
            TransitionTimeout = TimeSpan.FromSeconds(1),
            OverallOperationTimeout = TimeSpan.FromSeconds(2)
        };
        authorizationStore = new ServiceMutationAuthorizationStore(options, TimeProvider.System);
        return new(
            manager,
            audit ?? new RecordingAuditSink(),
            new ServiceMutationCoordinator(options, TimeProvider.System),
            options,
            TimeProvider.System,
            authorizationStore);
    }

    private ServiceControlRequest Request(
        string serviceId,
        ServiceControlAction action,
        string? confirmation,
        string key,
        InvocationSource source = InvocationSource.LocalWpf,
        InvocationAuthorizationLevel level = InvocationAuthorizationLevel.LocalAdministrator)
    {
        var context = new InvocationContext(source, "S-1-5-21-1000", level, $"corr-{key}");
        var authorization = authorizationStore?.Issue(
            context.AuthenticatedCaller,
            serviceId,
            action,
            confirmation ?? string.Empty,
            context.CorrelationId);
        return new(
            context,
            serviceId,
            action,
            confirmation,
            key,
            authorization?.Token ?? string.Empty);
    }

    private sealed class FakeServiceManager(ServiceStatus initial) : IServiceManager
    {
        private readonly object gate = new();
        private ServiceStatus state = initial;
        public ConcurrentQueue<(string ServiceName, ServiceControlAction Action)> Calls { get; } = new();

        public Task<ServiceStatus> GetStatusAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (gate) return Task.FromResult(state);
        }

        public Task<IReadOnlyDictionary<string, ServiceStatus>> GetStatusesAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (gate)
            {
                return Task.FromResult<IReadOnlyDictionary<string, ServiceStatus>>(
                    serviceNames.ToDictionary(name => name, _ => state, StringComparer.OrdinalIgnoreCase));
            }
        }

        public Task ControlAsync(string serviceName, ServiceControlAction action, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Enqueue((serviceName, action));
            lock (gate)
            {
                state = action == ServiceControlAction.Start ? ServiceStatus.Running : ServiceStatus.Stopped;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAuditSink : IAgentAuditSink
    {
        public List<AgentAuditEvent> Events { get; } = [];
        public string? FailOutcome { get; init; }

        public bool Record(AgentAuditEvent auditEvent)
        {
            Events.Add(auditEvent);
            return !string.Equals(FailOutcome, auditEvent.Outcome, StringComparison.Ordinal);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan amount) => now += amount;
    }
}
