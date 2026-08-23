using System.IO;
using System.Security;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public sealed class LocalAgentServiceHealthClient(LocalIpcClient localIpcClient)
    : ILocalServiceHealthClient
{
    public async Task<ServiceHealthResult> GetHealthAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await localIpcClient
                .GetServiceHealthAsync(correlationId, cancellationToken)
                .ConfigureAwait(false);

            if (!response.Succeeded)
            {
                return MapFailure(response.ErrorCode, response.CorrelationId);
            }

            if (response.Result is not { } snapshot
                || !Enum.IsDefined(snapshot.OverallState)
                || snapshot.Services is null)
            {
                return ServiceHealthResult.Failure(
                    ServiceHealthViewState.InvalidResponse,
                    "invalid_response",
                    "The Agent returned an invalid service health response.",
                    response.CorrelationId);
            }

            var rows = new List<ServiceHealthRow>(snapshot.Services.Count);
            foreach (var item in snapshot.Services)
            {
                if (!ServiceHealthRow.TryCreate(item, out var row) || row is null)
                {
                    return ServiceHealthResult.Failure(
                        ServiceHealthViewState.InvalidResponse,
                        "invalid_response",
                        "The Agent returned an invalid service health response.",
                        response.CorrelationId);
                }

                rows.Add(row);
            }

            var state = snapshot.OverallState switch
            {
                ServiceHealthOverallState.Healthy => ServiceHealthViewState.Healthy,
                ServiceHealthOverallState.Degraded => ServiceHealthViewState.Degraded,
                ServiceHealthOverallState.Unknown => ServiceHealthViewState.Unknown,
                _ => ServiceHealthViewState.InvalidResponse
            };
            if (state == ServiceHealthViewState.InvalidResponse)
            {
                return ServiceHealthResult.Failure(
                    ServiceHealthViewState.InvalidResponse,
                    "invalid_response",
                    "The Agent returned an invalid service health response.",
                    response.CorrelationId);
            }

            return ServiceHealthResult.Healthy(
                state,
                rows,
                snapshot.CheckedAtUtc,
                response.CorrelationId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ServiceHealthResult.Failure(
                ServiceHealthViewState.TimedOut,
                "service_health_timeout",
                "Service health check timed out.",
                correlationId);
        }
        catch (LocalIpcProtocolMismatchException)
        {
            return ServiceHealthResult.Failure(
                ServiceHealthViewState.ProtocolMismatch,
                "protocol_mismatch",
                "Desktop and Agent versions are not compatible.",
                correlationId);
        }
        catch (LocalIpcServerIdentityException)
        {
            return ServiceHealthResult.Failure(
                ServiceHealthViewState.SecurityVerificationFailed,
                "security_verification_failed",
                "The local Agent connection could not be verified.",
                correlationId);
        }
        catch (LocalIpcProtocolException)
        {
            return ServiceHealthResult.Failure(
                ServiceHealthViewState.InvalidResponse,
                "invalid_response",
                "The Agent returned an invalid service health response.",
                correlationId);
        }
        catch (UnauthorizedAccessException)
        {
            return ServiceHealthResult.Failure(
                ServiceHealthViewState.SecurityVerificationFailed,
                "security_verification_failed",
                "The local Agent connection could not be verified.",
                correlationId);
        }
        catch (SecurityException)
        {
            return ServiceHealthResult.Failure(
                ServiceHealthViewState.SecurityVerificationFailed,
                "security_verification_failed",
                "The local Agent connection could not be verified.",
                correlationId);
        }
        catch (IOException)
        {
            return ServiceHealthResult.Failure(
                ServiceHealthViewState.Unavailable,
                "agent_unavailable",
                "RMS Support Agent is not available on this machine.",
                correlationId);
        }
        catch (Exception)
        {
            return ServiceHealthResult.Failure(
                ServiceHealthViewState.UnknownError,
                "service_health_failed",
                "Service health could not be determined.",
                correlationId);
        }
    }

    private static ServiceHealthResult MapFailure(string? errorCode, string correlationId) =>
        errorCode?.Trim().ToLowerInvariant() switch
        {
            "service_health_timeout" or "request_timeout" => ServiceHealthResult.Failure(
                ServiceHealthViewState.TimedOut,
                "service_health_timeout",
                "Service health check timed out.",
                correlationId),
            "protocol_mismatch" or "unsupported_protocol_version" or "unknown_operation" => ServiceHealthResult.Failure(
                ServiceHealthViewState.ProtocolMismatch,
                "protocol_mismatch",
                "Desktop and Agent versions are not compatible.",
                correlationId),
            "security_verification_failed" or "unauthorized" => ServiceHealthResult.Failure(
                ServiceHealthViewState.SecurityVerificationFailed,
                "security_verification_failed",
                "The local Agent connection could not be verified.",
                correlationId),
            "invalid_response" or "malformed_response" => ServiceHealthResult.Failure(
                ServiceHealthViewState.InvalidResponse,
                "invalid_response",
                "The Agent returned an invalid service health response.",
                correlationId),
            "service_health_unavailable" => ServiceHealthResult.Failure(
                ServiceHealthViewState.Unavailable,
                "service_health_unavailable",
                "RMS service health is currently unavailable.",
                correlationId),
            "agent_unavailable" => ServiceHealthResult.Failure(
                ServiceHealthViewState.Unavailable,
                "agent_unavailable",
                "RMS Support Agent is not available on this machine.",
                correlationId),
            _ => ServiceHealthResult.Failure(
                ServiceHealthViewState.UnknownError,
                "service_health_failed",
                "Service health could not be determined.",
                correlationId)
        };
}
