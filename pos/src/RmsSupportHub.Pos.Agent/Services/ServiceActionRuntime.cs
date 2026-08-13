using Microsoft.AspNetCore.Http;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Exceptions;
using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Agent.Services;

public static class ServiceActionOperation
{
    public const string OperationId = "services.control";
    public const string HttpMethod = "POST";
    public const string HttpPathTemplate = "/api/v1/services/{targetId}/actions";

    public static MutationOperationDescriptor Descriptor { get; } = new(
        OperationId,
        HttpMethod,
        HttpPathTemplate,
        MutationTargetKind.AllowListedService);
}

public enum ServiceActionSecurityFailure
{
    None,
    PrincipalUnavailable,
    MutationTokenInvalid
}

public sealed record ServiceActionExecutionResult(
    ServiceActionResponseDto? Response,
    ServiceActionSecurityFailure SecurityFailure = ServiceActionSecurityFailure.None);

/// <summary>
/// Runs exactly one typed Start/Stop/Restart request after every server-side gate has passed. The
/// token is consumed only after target, state, idempotency, concurrency, and identity checks and
/// immediately before the IServiceManager dispatch boundary.
/// </summary>
public sealed class ServiceActionRuntime(
    ServiceAllowList allowList,
    IServiceManager manager,
    IMutationTokenStore mutationTokens,
    IAgentPrincipalSidResolver principalSidResolver,
    ServiceActionIdempotencyStore idempotency,
    ServiceActionConcurrencyGate concurrency,
    ServiceActionOptions options)
{
    public async Task<ServiceActionExecutionResult> ExecuteAsync(
        HttpContext context,
        string? serviceId,
        ServiceActionRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = CorrelationIdContext.TryGet(context) ?? "unavailable";
        if (request is null)
        {
            return NotAttempted(correlationId, ServiceActionCodes.InvalidRequest, "The service action request is required.");
        }

        if (!Enum.IsDefined(request.Action)
            || request.Action is not (ServiceActionKind.Start or ServiceActionKind.Stop or ServiceActionKind.Restart))
        {
            return NotAttempted(correlationId, ServiceActionCodes.InvalidRequest, "The requested service action is not supported.");
        }

        if (!IsValidIdempotencyKey(request.IdempotencyKey))
        {
            return NotAttempted(correlationId, ServiceActionCodes.InvalidIdempotencyKey, "A bounded idempotency key is required.");
        }

        AllowListedService? target;
        try
        {
            target = await allowList.ResolveAsync(serviceId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return NotAttempted(correlationId, ServiceActionCodes.TargetNotAllowListed, "The service action was not attempted.");
        }
        catch
        {
            return NotAttempted(correlationId, ServiceActionCodes.TargetNotAllowListed, "The requested service is not available to this Agent.");
        }

        if (target is null)
        {
            return NotAttempted(correlationId, ServiceActionCodes.TargetNotAllowListed, "The requested service is not available to this Agent.");
        }

        var lease = concurrency.TryEnter(target.ServiceId);
        if (lease is null)
        {
            return NotAttempted(correlationId, ServiceActionCodes.ServiceBusy, "Another action for this service is already in progress.");
        }

        // The per-service gate makes reservation and dispatch ordering deterministic. No caller
        // identity participates in this key.
        var reservation = idempotency.TryReserve(target.ServiceId, request.IdempotencyKey, request.Action);
        if (reservation.State is ServiceActionReservationState.Completed && reservation.ExistingResponse is { } existing)
        {
            lease.Dispose();
            return new(existing);
        }

        if (reservation.State is ServiceActionReservationState.InProgress)
        {
            lease.Dispose();
            return NotAttempted(correlationId, ServiceActionCodes.DuplicateInProgress, "An identical service action is already in progress.");
        }

        if (reservation.State is ServiceActionReservationState.Conflict)
        {
            lease.Dispose();
            return NotAttempted(correlationId, ServiceActionCodes.IdempotencyConflict, "The idempotency key is already bound to another service action.");
        }

        if (reservation.State is ServiceActionReservationState.Capacity)
        {
            lease.Dispose();
            return NotAttempted(correlationId, ServiceActionCodes.IdempotencyCapacity, "The Agent cannot retain another service action at this time.");
        }

        var reservationHeld = true;
        var leaseHeld = true;
        try
        {
            ServiceStatus currentState;
            try
            {
                currentState = await manager.GetStatusAsync(target.ServiceName, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return NotAttempted(correlationId, ServiceActionCodes.StateUnavailable, "The service state could not be checked; no action was attempted.");
            }
            catch
            {
                return NotAttempted(correlationId, ServiceActionCodes.StateUnavailable, "The service state could not be checked; no action was attempted.");
            }

            if (currentState is not (ServiceStatus.Running or ServiceStatus.Stopped))
            {
                return NotAttempted(correlationId, ServiceActionCodes.StateUnavailable, "The service state is not available for a control action.");
            }

            if (!ReadOnlyServiceStatusService.AllowedActionsFor(currentState).Contains(request.Action))
            {
                return NotAttempted(correlationId, ServiceActionCodes.ActionNotAllowed, "That action is not available for the current service state.");
            }

            if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
            {
                return new(null, ServiceActionSecurityFailure.PrincipalUnavailable);
            }

            var tokenValues = context.Request.Headers[MutationTokenContract.HeaderName];
            var originValues = context.Request.Headers.Origin;
            if (tokenValues.Count != 1 || originValues.Count != 1 || string.IsNullOrWhiteSpace(tokenValues[0]) || string.IsNullOrWhiteSpace(originValues[0]))
            {
                idempotency.Release(target.ServiceId, request.IdempotencyKey, request.Action);
                reservationHeld = false;
                return new(null, ServiceActionSecurityFailure.MutationTokenInvalid);
            }

            if (!ServiceActionOperation.Descriptor.TryResolveHttpPath(target.ServiceId, out var expectedPath)
                || !string.Equals(context.Request.Method, ServiceActionOperation.HttpMethod, StringComparison.Ordinal)
                || !string.Equals(context.Request.Path.Value, expectedPath, StringComparison.Ordinal))
            {
                idempotency.Release(target.ServiceId, request.IdempotencyKey, request.Action);
                reservationHeld = false;
                return new(null, ServiceActionSecurityFailure.MutationTokenInvalid);
            }

            // This is intentionally the final gate. There is no await or policy decision between
            // successful consumption and the typed IServiceManager call below.
            var consumed = mutationTokens.TryConsume(new MutationTokenValidationRequest(
                tokenValues[0]!,
                principalSid,
                originValues[0]!,
                context.Request.Method,
                ServiceActionOperation.OperationId,
                context.Request.Path.Value));
            if (!consumed.Succeeded)
            {
                idempotency.Release(target.ServiceId, request.IdempotencyKey, request.Action);
                reservationHeld = false;
                return new(null, ServiceActionSecurityFailure.MutationTokenInvalid);
            }

            using var dispatchTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            dispatchTimeout.CancelAfter(options.DispatchTimeout);
            var domainAction = MapAction(request.Action);
            Task dispatchTask;
            try
            {
                dispatchTask = manager.ControlAsync(target.ServiceName, domainAction, dispatchTimeout.Token);
            }
            catch (ServiceControlRejectedException)
            {
                var response = Failed(correlationId);
                idempotency.Complete(target.ServiceId, request.IdempotencyKey, request.Action, response);
                reservationHeld = false;
                return new(response);
            }
            catch
            {
                var response = Unknown(correlationId);
                idempotency.Complete(target.ServiceId, request.IdempotencyKey, request.Action, response);
                reservationHeld = false;
                return new(response);
            }

            var timeoutTask = Task.Delay(options.DispatchTimeout);
            var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            var completed = await Task.WhenAny(dispatchTask, timeoutTask, cancellationTask).ConfigureAwait(false);
            if (completed != dispatchTask)
            {
                dispatchTimeout.Cancel();
                var response = Unknown(correlationId);
                idempotency.Complete(target.ServiceId, request.IdempotencyKey, request.Action, response);
                reservationHeld = false;
                leaseHeld = false;
                _ = ReleaseLeaseAfterDispatchAsync(dispatchTask, lease);
                return new(response);
            }

            try
            {
                await dispatchTask.ConfigureAwait(false);
                var response = Accepted(correlationId);
                idempotency.Complete(target.ServiceId, request.IdempotencyKey, request.Action, response);
                reservationHeld = false;
                return new(response);
            }
            catch (ServiceControlRejectedException)
            {
                var response = Failed(correlationId);
                idempotency.Complete(target.ServiceId, request.IdempotencyKey, request.Action, response);
                reservationHeld = false;
                return new(response);
            }
            catch (OperationCanceledException)
            {
                var response = Unknown(correlationId);
                idempotency.Complete(target.ServiceId, request.IdempotencyKey, request.Action, response);
                reservationHeld = false;
                return new(response);
            }
            catch
            {
                var response = Unknown(correlationId);
                idempotency.Complete(target.ServiceId, request.IdempotencyKey, request.Action, response);
                reservationHeld = false;
                return new(response);
            }
        }
        finally
        {
            if (reservationHeld)
            {
                idempotency.Release(target.ServiceId, request.IdempotencyKey, request.Action);
            }

            if (leaseHeld)
            {
                lease.Dispose();
            }
        }
    }

    public static bool IsValidIdempotencyKey(string? key) =>
        !string.IsNullOrWhiteSpace(key)
        && key.Length <= ServiceActionOptions.MaxIdempotencyKeyLength
        && key.All(character => character is >= '!' and <= '~');

    private static ServiceControlAction MapAction(ServiceActionKind action) => action switch
    {
        ServiceActionKind.Start => ServiceControlAction.Start,
        ServiceActionKind.Stop => ServiceControlAction.Stop,
        ServiceActionKind.Restart => ServiceControlAction.Restart,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static ServiceActionExecutionResult NotAttempted(
        string correlationId,
        string code,
        string detail) =>
        new(new(ServiceActionOutcome.NotAttempted, code, detail, correlationId));

    private static ServiceActionResponseDto Accepted(string correlationId) =>
        new(ServiceActionOutcome.Accepted, ServiceActionCodes.Accepted, "The Agent acknowledged the service action.", correlationId);

    private static ServiceActionResponseDto Failed(string correlationId) =>
        new(ServiceActionOutcome.Failed, ServiceActionCodes.Failed, "The Windows service rejected the action definitively.", correlationId);

    private static ServiceActionResponseDto Unknown(string correlationId) =>
        new(ServiceActionOutcome.OutcomeUnknown, ServiceActionCodes.OutcomeUnknown, "The service action outcome is unknown. Check the service state before deciding whether to retry.", correlationId);

    private static async Task ReleaseLeaseAfterDispatchAsync(Task dispatchTask, ServiceActionLease lease)
    {
        try
        {
            await dispatchTask.ConfigureAwait(false);
        }
        catch
        {
            // The response has already recorded OutcomeUnknown. The background observation only
            // keeps the same-service concurrency gate closed until the original call ends.
        }
        finally
        {
            lease.Dispose();
        }
    }
}
