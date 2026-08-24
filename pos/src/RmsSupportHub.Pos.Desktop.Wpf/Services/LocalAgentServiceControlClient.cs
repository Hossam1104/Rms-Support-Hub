using System.IO;
using System.Security;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public sealed class LocalAgentServiceControlClient(LocalIpcClient localIpcClient)
    : ILocalServiceControlClient
{
    public async Task<ServiceControlAuthorizationResult> GetAuthorizationAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await localIpcClient
                .GetAuthorizationAsync(correlationId, cancellationToken)
                .ConfigureAwait(false);
            if (!response.Succeeded || response.Result is not { } value
                || !IsValidAuthorization(value))
            {
                return ServiceControlAuthorizationResult.Unavailable(
                    response.ErrorCode ?? "invalid_response",
                    "The Agent returned an invalid authorization response.");
            }

            return new(
                value.AuthorizationLevel == "LocalAdministrator",
                value.CanManageRmsServices,
                string.Empty,
                string.Empty);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ServiceControlAuthorizationResult.Unavailable("cancelled", "Service authorization was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return ServiceControlAuthorizationResult.Unavailable("service_control_timeout", "Service authorization timed out.");
        }
        catch (LocalIpcProtocolMismatchException)
        {
            return ServiceControlAuthorizationResult.Unavailable("protocol_mismatch", "Desktop and Agent versions are not compatible.");
        }
        catch (LocalIpcServerIdentityException)
        {
            return ServiceControlAuthorizationResult.Unavailable("security_verification_failed", "The local Agent connection could not be verified.");
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException)
        {
            return ServiceControlAuthorizationResult.Unavailable("security_verification_failed", "The local Agent connection could not be verified.");
        }
        catch (IOException)
        {
            return ServiceControlAuthorizationResult.Unavailable("agent_unavailable", "RMS Support Agent is not available on this machine.");
        }
        catch
        {
            return ServiceControlAuthorizationResult.Unavailable("authorization_failed", "Service authorization could not be determined.");
        }
    }

    public async Task<ServiceActionResult> ExecuteAsync(
        string serviceId,
        ServiceActionKind action,
        string? confirmation,
        string correlationId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var authorizationResponse = await localIpcClient
                .IssueServiceActionAuthorizationAsync(
                    new LocalIpcServiceActionAuthorizationRequestDto(serviceId, action, confirmation),
                    correlationId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!authorizationResponse.Succeeded
                || authorizationResponse.Result is not { } authorization
                || !IsValidAuthorization(authorization, serviceId, action, correlationId))
            {
                return ServiceActionResult.Failure(
                    authorizationResponse.ErrorCode is "administrator_authorization_required" or "mutation_authorization_invalid"
                        ? ServiceControlViewState.Unauthorized
                        : ServiceControlViewState.InvalidResponse,
                    authorizationResponse.ErrorCode ?? "invalid_response",
                    authorizationResponse.ErrorMessage ?? "The Agent could not authorize the service action.",
                    serviceId,
                    action,
                    correlationId);
            }

            var response = await localIpcClient
                .ExecuteServiceActionAsync(
                    new LocalIpcServiceActionRequestDto(serviceId, action, confirmation, idempotencyKey, authorization.MutationAuthorization),
                    correlationId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!response.Succeeded || response.Result is not { } value
                || !IsValidResponse(value, serviceId, action, correlationId))
            {
                return ServiceActionResult.Failure(
                    ServiceControlViewState.InvalidResponse,
                    response.ErrorCode ?? "invalid_response",
                    "The Agent returned an invalid service-control response.",
                    serviceId,
                    action,
                    correlationId);
            }

            return Map(value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ServiceActionResult.Failure(ServiceControlViewState.Cancelled, "cancelled", "The service action was cancelled.", serviceId, action, correlationId);
        }
        catch (OperationCanceledException)
        {
            return ServiceActionResult.Failure(ServiceControlViewState.TimedOut, "service_control_timeout", "The service action timed out.", serviceId, action, correlationId);
        }
        catch (LocalIpcProtocolMismatchException)
        {
            return ServiceActionResult.Failure(ServiceControlViewState.InvalidResponse, "protocol_mismatch", "Desktop and Agent versions are not compatible.", serviceId, action, correlationId);
        }
        catch (LocalIpcServerIdentityException)
        {
            return ServiceActionResult.Failure(ServiceControlViewState.Unavailable, "security_verification_failed", "The local Agent connection could not be verified.", serviceId, action, correlationId);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException)
        {
            return ServiceActionResult.Failure(ServiceControlViewState.Unauthorized, "administrator_authorization_required", "Administrator authority is required to control RMS services.", serviceId, action, correlationId);
        }
        catch (LocalIpcProtocolException)
        {
            return ServiceActionResult.Failure(ServiceControlViewState.InvalidResponse, "invalid_response", "The Agent returned an invalid service-control response.", serviceId, action, correlationId);
        }
        catch (IOException)
        {
            return ServiceActionResult.Failure(ServiceControlViewState.Unavailable, "agent_unavailable", "RMS Support Agent is not available on this machine.", serviceId, action, correlationId);
        }
        catch
        {
            return ServiceActionResult.Failure(ServiceControlViewState.Failed, "service_control_failed", "The service action could not be completed.", serviceId, action, correlationId);
        }
    }

    private static bool IsValidAuthorization(LocalIpcAuthorizationDto value) =>
        value.AuthorizationLevel is "LocalOperator" or "LocalAdministrator" or "Unauthenticated"
        && (!value.CanManageRmsServices || value.AuthorizationLevel == "LocalAdministrator");

    private static bool IsValidAuthorization(
        LocalIpcServiceActionAuthorizationResponseDto value,
        string serviceId,
        ServiceActionKind action,
        string correlationId) =>
        IsSafeText(value.MutationAuthorization, 256)
        && value.ServiceId == serviceId
        && value.Action == action
        && value.CorrelationId == correlationId
        && IsSafeText(value.CorrelationId, 128)
        && value.ExpiresAtUtc > DateTimeOffset.UtcNow;

    private static bool IsValidResponse(
        LocalIpcServiceActionResponseDto value,
        string serviceId,
        ServiceActionKind action,
        string correlationId) =>
        IsSafeId(value.OperationId)
        && value.ServiceId == serviceId
        && value.Action == action
        && Enum.IsDefined(value.State)
        && value.ProgressPercent is >= 0 and <= 100
        && IsSafeText(value.Stage, 64)
        && (!value.ObservedState.HasValue || Enum.IsDefined(value.ObservedState.Value))
        && IsSafeText(value.Code, 128)
        && IsSafeText(value.Detail, 512)
        && value.CorrelationId == correlationId
        && IsSafeText(value.CorrelationId, 128);

    private static ServiceActionResult Map(LocalIpcServiceActionResponseDto value)
    {
        var state = value.State switch
        {
            LocalIpcServiceActionState.Completed => ServiceControlViewState.Completed,
            LocalIpcServiceActionState.OutcomeUnknown => ServiceControlViewState.OutcomeUnknown,
            LocalIpcServiceActionState.Cancelled => ServiceControlViewState.Cancelled,
            LocalIpcServiceActionState.Failed => value.Code is "administrator_authorization_required" or "diagnostic_authorization_required"
                ? ServiceControlViewState.Unauthorized
                : ServiceControlViewState.Failed,
            LocalIpcServiceActionState.Accepted or LocalIpcServiceActionState.Running => value.Action switch
            {
                ServiceActionKind.Start => ServiceControlViewState.Starting,
                ServiceActionKind.Stop => ServiceControlViewState.Stopping,
                _ => ServiceControlViewState.Restarting
            },
            _ => ServiceControlViewState.InvalidResponse
        };
        return new(
            state,
            value.OperationId,
            value.ServiceId,
            value.Action,
            value.ProgressPercent,
            value.Stage,
            value.ObservedState,
            value.Code,
            value.Detail,
            value.CorrelationId,
            value.RecoveryRequired);
    }

    private static bool IsSafeId(string value) =>
        value.Length == 32 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsSafeText(string value, int maximum) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= maximum
        && !value.Any(char.IsControl);
}
