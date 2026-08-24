using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Exceptions;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Services;

public sealed class ServiceControlOptions
{
    public int MaxIdempotencyEntries { get; init; } = 256;

    public int MaxMutationAuthorizations { get; init; } = 256;

    public TimeSpan IdempotencyRetention { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan MutationAuthorizationLifetime { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan TransitionTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(150);

    public TimeSpan OverallOperationTimeout { get; init; } = TimeSpan.FromSeconds(45);

    public void Validate()
    {
        if (MaxIdempotencyEntries is < 1 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxIdempotencyEntries));
        }

        if (MaxMutationAuthorizations is < 1 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxMutationAuthorizations));
        }

        if (IdempotencyRetention <= TimeSpan.Zero || IdempotencyRetention > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(IdempotencyRetention));
        }

        if (MutationAuthorizationLifetime <= TimeSpan.Zero
            || MutationAuthorizationLifetime > TimeSpan.FromMinutes(15))
        {
            throw new ArgumentOutOfRangeException(nameof(MutationAuthorizationLifetime));
        }

        if (TransitionTimeout <= TimeSpan.Zero || TransitionTimeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(TransitionTimeout));
        }

        if (PollInterval <= TimeSpan.Zero || PollInterval > TransitionTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(PollInterval));
        }

        if (OverallOperationTimeout < TransitionTimeout
            || OverallOperationTimeout > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(nameof(OverallOperationTimeout));
        }
    }
}

public sealed record ServiceControlRequest(
    InvocationContext Context,
    string ServiceId,
    ServiceControlAction Action,
    string? Confirmation,
    string IdempotencyKey,
    string MutationAuthorization);

public enum ServiceControlReservationState
{
    Reserved,
    Completed,
    InProgress,
    Conflict,
    Capacity,
    Busy
}

public readonly record struct ServiceControlReservation(
    ServiceControlReservationState State,
    ServiceControlOperationResult? ExistingResult = null,
    ServiceMutationCoordinator.ServiceMutationLease? Lease = null);

/// <summary>
/// One bounded coordinator for service mutation leases and idempotency. Idle target references and
/// completed keys are pruned under the same lock so rejected or completed requests cannot grow
/// process memory without bound.
/// </summary>
public sealed class ServiceMutationCoordinator
{
    private readonly object gate = new();
    private readonly ServiceControlOptions options;
    private readonly TimeProvider clock;
    private readonly Dictionary<IdempotencyKey, Entry> entries = new();
    private readonly Dictionary<string, int> activeTargets = new(StringComparer.Ordinal);

    public ServiceMutationCoordinator(ServiceControlOptions options, TimeProvider clock)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public ServiceControlReservation TryBegin(
        string principal,
        string serviceId,
        ServiceControlAction action,
        string confirmation,
        string idempotencyKey,
        string correlationId)
    {
        var key = new IdempotencyKey(principal, serviceId, idempotencyKey);
        var now = clock.GetUtcNow();
        lock (gate)
        {
            PruneLocked(now);
            if (entries.TryGetValue(key, out var existing))
            {
                if (existing.Action != action
                    || !string.Equals(existing.Confirmation, confirmation, StringComparison.Ordinal)
                    || !string.Equals(existing.CorrelationId, correlationId, StringComparison.Ordinal))
                {
                    return new(ServiceControlReservationState.Conflict);
                }

                return existing.Result is { } result
                    ? new(ServiceControlReservationState.Completed, result)
                    : new(ServiceControlReservationState.InProgress);
            }

            if (entries.Count >= options.MaxIdempotencyEntries)
            {
                return new(ServiceControlReservationState.Capacity);
            }

            if (activeTargets.ContainsKey(serviceId))
            {
                return new(ServiceControlReservationState.Busy);
            }

            entries.Add(key, new(action, confirmation, correlationId, now, null));
            activeTargets[serviceId] = 1;
            return new(ServiceControlReservationState.Reserved, Lease: new(this, key, serviceId));
        }
    }

    public void Complete(
        string principal,
        string serviceId,
        string idempotencyKey,
        ServiceControlOperationResult result)
    {
        var key = new IdempotencyKey(principal, serviceId, idempotencyKey);
        lock (gate)
        {
            if (entries.TryGetValue(key, out var existing))
            {
                entries[key] = existing with { Result = result };
            }
        }
    }

    public void Release(
        string principal,
        string serviceId,
        string idempotencyKey)
    {
        var key = new IdempotencyKey(principal, serviceId, idempotencyKey);
        lock (gate)
        {
            entries.Remove(key);
            ReleaseTargetLocked(serviceId);
        }
    }

    public int RetainedEntryCount
    {
        get
        {
            lock (gate)
            {
                PruneLocked(clock.GetUtcNow());
                return entries.Count;
            }
        }
    }

    public int ActiveTargetCount
    {
        get
        {
            lock (gate)
            {
                return activeTargets.Count;
            }
        }
    }

    private void ReleaseLease(IdempotencyKey key, string serviceId)
    {
        lock (gate)
        {
            if (entries.TryGetValue(key, out var entry) && entry.Result is null)
            {
                entries.Remove(key);
            }

            ReleaseTargetLocked(serviceId);
        }
    }

    private void ReleaseTargetLocked(string serviceId)
    {
        if (!activeTargets.TryGetValue(serviceId, out var count))
        {
            return;
        }

        if (count <= 1)
        {
            activeTargets.Remove(serviceId);
        }
        else
        {
            activeTargets[serviceId] = count - 1;
        }
    }

    private void PruneLocked(DateTimeOffset now)
    {
        foreach (var pair in entries.ToArray())
        {
            if (pair.Value.Result is not null
                && now - pair.Value.CreatedAtUtc >= options.IdempotencyRetention)
            {
                entries.Remove(pair.Key);
            }
        }
    }

    internal readonly record struct IdempotencyKey(string Principal, string ServiceId, string Value);

    private sealed record Entry(
        ServiceControlAction Action,
        string Confirmation,
        string CorrelationId,
        DateTimeOffset CreatedAtUtc,
        ServiceControlOperationResult? Result);

    public sealed class ServiceMutationLease : IDisposable
    {
        private readonly ServiceMutationCoordinator owner;
        private readonly IdempotencyKey key;
        private readonly string serviceId;
        private int released;

        internal ServiceMutationLease(ServiceMutationCoordinator owner, IdempotencyKey key, string serviceId)
        {
            this.owner = owner;
            this.key = key;
            this.serviceId = serviceId;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
            {
                owner.ReleaseLease(key, serviceId);
            }
        }
    }
}

public sealed class ServiceControlApplicationService(
    IServiceManager serviceManager,
    IAgentAuditSink audit,
    ServiceMutationCoordinator coordinator,
    ServiceControlOptions options,
    TimeProvider clock,
    ServiceMutationAuthorizationStore? authorizationStore = null)
{
    private const string Operation = "rms.service-control";
    private readonly ServiceMutationAuthorizationStore mutationAuthorizations =
        authorizationStore ?? new ServiceMutationAuthorizationStore(options, clock);

    public ServiceMutationAuthorizationIssue IssueMutationAuthorization(
        InvocationContext context,
        string serviceId,
        ServiceControlAction action,
        string? confirmation)
    {
        var decision = AgentOperationAuthorization.Authorize(context, AgentOperationRisk.AdministratorOnlyMutation);
        if (!decision.Allowed)
        {
            return ServiceMutationAuthorizationIssue.Failure(decision.Code, decision.Message);
        }

        var validation = ValidateServiceAction(serviceId, action, confirmation);
        if (validation is not null)
        {
            return ServiceMutationAuthorizationIssue.Failure(validation.Value.Code, validation.Value.Detail);
        }

        if (!RmsServiceCatalog.TryResolveServiceId(serviceId, out var definition)
            || definition is null)
        {
            return ServiceMutationAuthorizationIssue.Failure(
                "service_not_found",
                "The requested RMS service is not available to this Agent.");
        }

        if (!IsSafeCorrelationId(context.CorrelationId))
        {
            return ServiceMutationAuthorizationIssue.Failure(
                "invocation_context_invalid",
                "The authenticated invocation context is invalid.");
        }

        return mutationAuthorizations.Issue(
            context.AuthenticatedCaller,
            serviceId,
            action,
            confirmation ?? string.Empty,
            context.CorrelationId);
    }

    public async Task<ServiceControlOperationResult> ExecuteAsync(
        ServiceControlRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        options.Validate();

        var operationId = Guid.NewGuid().ToString("N");
        var context = request.Context ?? throw new ArgumentException("An invocation context is required.", nameof(request));
        var correlationId = context.CorrelationId;
        var preflight = ValidateRequest(request);
        if (preflight is not null)
        {
            return preflight with { OperationId = operationId };
        }

        if (!RmsServiceCatalog.TryResolveServiceId(request.ServiceId, out var definition)
            || definition is null)
        {
            return Result(operationId, request, ServiceControlOperationState.Failed, 0, "preflight", null, "service_not_found", "The requested RMS service is not available to this Agent.");
        }

        var reservation = coordinator.TryBegin(
            context.AuthenticatedCaller,
            request.ServiceId,
            request.Action,
            request.Confirmation ?? string.Empty,
            request.IdempotencyKey,
            correlationId);
        if (reservation.State == ServiceControlReservationState.Completed && reservation.ExistingResult is { } existing)
        {
            return existing;
        }

        if (reservation.State != ServiceControlReservationState.Reserved || reservation.Lease is null)
        {
            return reservation.State switch
            {
                ServiceControlReservationState.InProgress => Result(operationId, request, ServiceControlOperationState.Accepted, 5, "queued", null, "operation_in_progress", "An identical service action is already in progress."),
                ServiceControlReservationState.Conflict => Result(operationId, request, ServiceControlOperationState.Failed, 0, "preflight", null, "idempotency_conflict", "The request key is bound to a different service action."),
                ServiceControlReservationState.Capacity => Result(operationId, request, ServiceControlOperationState.Failed, 0, "preflight", null, "idempotency_capacity", "The Agent cannot retain another service action at this time."),
                _ => Result(operationId, request, ServiceControlOperationState.Failed, 0, "preflight", null, "operation_in_progress", "Another action for this service is already in progress.")
            };
        }

        using var lease = reservation.Lease;
        if (!IsSafeToken(request.MutationAuthorization))
        {
            return Result(
                operationId,
                request,
                ServiceControlOperationState.Failed,
                0,
                "authorization",
                null,
                "mutation_authorization_invalid",
                "A one-use service mutation authorization is required.");
        }

        var authorization = mutationAuthorizations.TryConsume(
            request.MutationAuthorization,
            context.AuthenticatedCaller,
            request.ServiceId,
            request.Action,
            request.Confirmation ?? string.Empty,
            correlationId);
        if (!authorization.Succeeded)
        {
            return Result(
                operationId,
                request,
                ServiceControlOperationState.Failed,
                0,
                "authorization",
                null,
                "mutation_authorization_invalid",
                "The one-use service mutation authorization is invalid, expired, or already consumed.",
                false);
        }

        var completed = false;
        var dispatched = false;
        try
        {
            if (!RecordAudit(context, request, "requested", null))
            {
                return Complete(operationId, request, lease, ServiceControlOperationState.Failed, 0, "audit", null, "audit_unavailable", "The service action was not started because required audit recording is unavailable.", false);
            }

            using var operationTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            operationTimeout.CancelAfter(options.OverallOperationTimeout);
            var token = operationTimeout.Token;
            var current = await ReadStateAsync(definition.ServiceName, token).ConfigureAwait(false);
            if (current is not (ServiceStatus.Running or ServiceStatus.Stopped))
            {
                return Complete(operationId, request, lease, ServiceControlOperationState.Failed, 10, "preflight", current, current == ServiceStatus.NotFound ? "service_not_found" : "service_state_unknown", "The service state is not safe for a control action.", false);
            }

            if (request.Action == ServiceControlAction.Start && current == ServiceStatus.Running)
            {
                return Complete(operationId, request, lease, ServiceControlOperationState.Completed, 100, "verified", current, "already_running", "The RMS service is already running.", false);
            }

            if (request.Action == ServiceControlAction.Stop && current == ServiceStatus.Stopped)
            {
                return Complete(operationId, request, lease, ServiceControlOperationState.Completed, 100, "verified", current, "already_stopped", "The RMS service is already stopped.", false);
            }

            if (!RecordAudit(context, request, "accepted", null)
                || !RecordAudit(context, request, "started", null)
                || !RecordAudit(context, request, "dispatch", null))
            {
                return Complete(operationId, request, lease, ServiceControlOperationState.Failed, 15, "audit", current, "audit_unavailable", "The service action was not dispatched because required audit recording is unavailable.", false);
            }

            if (token.IsCancellationRequested)
            {
                return Complete(operationId, request, lease, ServiceControlOperationState.Cancelled, 15, "cancelled", current, "cancelled", "The service action was cancelled before dispatch.", false);
            }

            try
            {
                if (request.Action == ServiceControlAction.Restart)
                {
                    dispatched = true;
                    await serviceManager
                        .ControlAsync(definition.ServiceName, ServiceControlAction.Stop, CancellationToken.None)
                        .WaitAsync(options.TransitionTimeout, CancellationToken.None)
                        .ConfigureAwait(false);
                    var stopped = await WaitForStateAsync(definition.ServiceName, ServiceStatus.Stopped, token).ConfigureAwait(false);
                    if (stopped != ServiceStatus.Stopped)
                    {
                        return Complete(operationId, request, lease, ServiceControlOperationState.OutcomeUnknown, 70, "recovery", stopped, "service_state_unknown", "Restart stopped dispatch without a verified stopped state.", true);
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return Complete(operationId, request, lease, ServiceControlOperationState.OutcomeUnknown, 75, "recovery", stopped, "operation_outcome_unknown", "Restart was cancelled after Stop; the service remains stopped until an administrator reviews it.", true);
                    }

                    await serviceManager
                        .ControlAsync(definition.ServiceName, ServiceControlAction.Start, CancellationToken.None)
                        .WaitAsync(options.TransitionTimeout, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                else
                {
                    dispatched = true;
                    await serviceManager
                        .ControlAsync(definition.ServiceName, request.Action, CancellationToken.None)
                        .WaitAsync(options.TransitionTimeout, CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
            catch (TimeoutException)
            {
                var observed = await ReadStateBestEffortAsync(definition.ServiceName).ConfigureAwait(false);
                return Complete(operationId, request, lease, ServiceControlOperationState.OutcomeUnknown, 75, "verifying", observed, "service_transition_timeout", "The service transition did not reach a known result within the bounded timeout.", true);
            }
            catch (ServiceControlRejectedException exception)
            {
                return Complete(operationId, request, lease, ServiceControlOperationState.Failed, 70, "failed", await ReadStateBestEffortAsync(definition.ServiceName).ConfigureAwait(false), SafeCode(exception.Code, "service_control_failed"), "The Windows service rejected the requested action.", false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                var observed = await ReadStateBestEffortAsync(definition.ServiceName).ConfigureAwait(false);
                return Complete(operationId, request, lease, ServiceControlOperationState.OutcomeUnknown, 75, "verifying", observed, "operation_outcome_unknown", "The service action was dispatched, but its final outcome is not yet certain.", request.Action == ServiceControlAction.Restart && observed != ServiceStatus.Running);
            }
            catch
            {
                return Complete(operationId, request, lease, ServiceControlOperationState.Failed, 70, "failed", await ReadStateBestEffortAsync(definition.ServiceName).ConfigureAwait(false), CodeFor(request.Action), "The service action could not be completed.", false);
            }

            ServiceStatus observedState;
            if (request.Action == ServiceControlAction.Restart)
            {
                var running = await WaitForStateAsync(definition.ServiceName, ServiceStatus.Running, token).ConfigureAwait(false);
                if (running != ServiceStatus.Running)
                {
                    return Complete(operationId, request, lease, ServiceControlOperationState.OutcomeUnknown, 90, "recovery", running, "operation_outcome_unknown", "Restart did not return the service to Running within the bound.", true);
                }

                observedState = running;
            }
            else
            {
                var desired = request.Action == ServiceControlAction.Stop
                    ? ServiceStatus.Stopped
                    : ServiceStatus.Running;
                observedState = await WaitForStateAsync(definition.ServiceName, desired, token).ConfigureAwait(false);
                if (observedState != desired)
                {
                    return Complete(operationId, request, lease, ServiceControlOperationState.OutcomeUnknown, 85, "verifying", observedState, "service_state_unknown", "The service action completed without a verified final state.", false);
                }
            }

            var success = Complete(operationId, request, lease, ServiceControlOperationState.Completed, 100, "verified", observedState, "completed", "The RMS service action completed and the final state was verified.", false);
            completed = true;
            return success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return dispatched
                ? Complete(operationId, request, lease, ServiceControlOperationState.OutcomeUnknown, 75, "verifying", await ReadStateBestEffortAsync(definition.ServiceName).ConfigureAwait(false), "operation_outcome_unknown", "The service action was dispatched, but its final outcome is not yet certain.", request.Action == ServiceControlAction.Restart)
                : Complete(operationId, request, lease, ServiceControlOperationState.Cancelled, 0, "cancelled", null, "cancelled", "The service action was cancelled before dispatch.", false);
        }
        catch (OperationCanceledException)
        {
            return dispatched
                ? Complete(operationId, request, lease, ServiceControlOperationState.OutcomeUnknown, 75, "verifying", await ReadStateBestEffortAsync(definition.ServiceName).ConfigureAwait(false), "service_transition_timeout", "The service action exceeded its bounded observation window.", request.Action == ServiceControlAction.Restart)
                : Complete(operationId, request, lease, ServiceControlOperationState.Cancelled, 0, "cancelled", null, "cancelled", "The service action was cancelled before dispatch.", false);
        }
        finally
        {
            if (!completed)
            {
                // The coordinator lease disposes the in-progress idempotency reservation. A
                // terminal result is stored by Complete before this method leaves the boundary.
            }
        }
    }

    private ServiceControlOperationResult? ValidateRequest(ServiceControlRequest request)
    {
        var decision = AgentOperationAuthorization.Authorize(request.Context, AgentOperationRisk.AdministratorOnlyMutation);
        if (!decision.Allowed)
        {
            return Result(string.Empty, request, ServiceControlOperationState.Failed, 0, "authorization", null, decision.Code, decision.Message);
        }

        var validation = ValidateServiceAction(request.ServiceId, request.Action, request.Confirmation);
        if (validation is not null)
        {
            return Result(string.Empty, request, ServiceControlOperationState.Failed, 0, validation.Value.Stage, null, validation.Value.Code, validation.Value.Detail);
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)
            || request.IdempotencyKey.Length > 128
            || request.IdempotencyKey.Any(character => character is < '!' or > '~'))
        {
            return Result(string.Empty, request, ServiceControlOperationState.Failed, 0, "preflight", null, "idempotency_key_invalid", "A bounded idempotency key is required.");
        }

        return null;
    }

    private static (string Stage, string Code, string Detail)? ValidateServiceAction(
        string serviceId,
        ServiceControlAction action,
        string? confirmation)
    {
        if (!ServiceIdentityCatalog.IsOpaqueServiceId(serviceId))
        {
            return ("preflight", "service_control_not_allowed", "Only fixed RMS service identities may be controlled.");
        }

        if (RmsServiceCatalog.IsAgentServiceId(serviceId))
        {
            return ("preflight", "agent_self_control_not_allowed", "The RMS Support Agent service cannot be controlled by this operation.");
        }

        if (!Enum.IsDefined(action))
        {
            return ("preflight", "service_control_not_allowed", "Only Start, Stop, and Restart are supported.");
        }

        var expectedConfirmation = action switch
        {
            ServiceControlAction.Stop => "STOP",
            ServiceControlAction.Restart => "RESTART",
            _ => null
        };
        return string.Equals(confirmation, expectedConfirmation, StringComparison.Ordinal)
            ? null
            : ("confirmation", expectedConfirmation is null ? "confirmation_not_allowed" : "confirmation_required", expectedConfirmation is null ? "Start does not require a confirmation token." : $"Explicit {expectedConfirmation} confirmation is required.");
    }

    private static bool IsSafeToken(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 256
        && value.All(character => character is >= '!' and <= '~');

    private static bool IsSafeCorrelationId(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(character => character is >= '!' and <= '~');

    private async Task<ServiceStatus> ReadStateAsync(string serviceName, CancellationToken cancellationToken)
    {
        try
        {
            return await serviceManager.GetStatusAsync(serviceName, cancellationToken).WaitAsync(options.TransitionTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return ServiceStatus.Unknown;
        }
    }

    private async Task<ServiceStatus> ReadStateBestEffortAsync(string serviceName)
    {
        try
        {
            return await serviceManager.GetStatusAsync(serviceName).WaitAsync(options.PollInterval).ConfigureAwait(false);
        }
        catch
        {
            return ServiceStatus.Unknown;
        }
    }

    private async Task<ServiceStatus> WaitForStateAsync(
        string serviceName,
        ServiceStatus expected,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.TransitionTimeout);
        while (!timeout.IsCancellationRequested)
        {
            ServiceStatus state;
            try
            {
                state = await ReadStateAsync(serviceName, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                break;
            }
            if (state == expected)
            {
                return state;
            }

            if (state is ServiceStatus.NotFound or ServiceStatus.Unknown)
            {
                return state;
            }

            await Task.Delay(options.PollInterval, timeout.Token).ConfigureAwait(false);
        }

        return await ReadStateBestEffortAsync(serviceName).ConfigureAwait(false);
    }

    private ServiceControlOperationResult Complete(
        string operationId,
        ServiceControlRequest request,
        ServiceMutationCoordinator.ServiceMutationLease lease,
        ServiceControlOperationState state,
        int progress,
        string stage,
        ServiceStatus? observedState,
        string code,
        string detail,
        bool recoveryRequired)
    {
        var result = Result(operationId, request, state, progress, stage, observedState, code, detail, recoveryRequired);
        var finalAuditSucceeded = RecordAudit(request.Context, request, ToAuditOutcome(state), code);
        if (!finalAuditSucceeded && state == ServiceControlOperationState.Completed)
        {
            result = result with
            {
                State = ServiceControlOperationState.OutcomeUnknown,
                Code = "audit_unavailable",
                Detail = "The service state was observed, but the final audit outcome could not be persisted.",
                RecoveryRequired = true
            };
        }

        coordinator.Complete(request.Context.AuthenticatedCaller, request.ServiceId, request.IdempotencyKey, result);
        return result;
    }

    private bool RecordAudit(
        InvocationContext context,
        ServiceControlRequest request,
        string outcome,
        string? failureCode)
    {
        try
        {
            return audit.Record(new AgentAuditEvent(
                clock.GetUtcNow(),
                context.AuthenticatedCaller,
                Operation,
                request.ServiceId,
                context.CorrelationId,
                outcome,
                failureCode,
                typeof(ServiceControlApplicationService).Assembly.GetName().Version?.ToString(3) ?? "unavailable",
                null));
        }
        catch
        {
            return false;
        }
    }

    private static string ToAuditOutcome(ServiceControlOperationState state) => state switch
    {
        ServiceControlOperationState.Completed => "completed",
        ServiceControlOperationState.Cancelled => "cancelled",
        ServiceControlOperationState.Failed => "failed",
        _ => "outcome_unknown"
    };

    private static string CodeFor(ServiceControlAction action) => action switch
    {
        ServiceControlAction.Start => "service_start_failed",
        ServiceControlAction.Stop => "service_stop_failed",
        ServiceControlAction.Restart => "service_restart_failed",
        _ => "service_control_failed"
    };

    private static string SafeCode(string value, string fallback) =>
        !string.IsNullOrWhiteSpace(value)
            && value.Length <= 64
            && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')
            ? value
            : fallback;

    private static ServiceControlOperationResult Result(
        string operationId,
        ServiceControlRequest request,
        ServiceControlOperationState state,
        int progress,
        string stage,
        ServiceStatus? observedState,
        string code,
        string detail,
        bool recoveryRequired = false) => new(
            operationId,
            request.ServiceId,
            request.Action,
            state,
            Math.Clamp(progress, 0, 100),
            stage,
            observedState,
            code,
            detail,
            request.Context.CorrelationId,
            recoveryRequired);
}
