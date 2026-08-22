using System.IO;
using System.Security;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public sealed class LocalAgentHealthClient(LocalIpcClient localIpcClient) : ILocalAgentHealthClient
{
    public async Task<AgentHealthResult> GetHealthAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await localIpcClient
                .GetHealthAsync(correlationId, cancellationToken)
                .ConfigureAwait(false);

            if (!response.Succeeded)
            {
                return MapAgentFailure(response.ErrorCode, response.CorrelationId);
            }

            if (response.Result is not { } health)
            {
                return AgentHealthResult.Failure(
                    HealthViewState.InvalidResponse,
                    "invalid_response",
                    "The Agent returned an invalid health response.",
                    response.CorrelationId);
            }

            if (health.ProtocolVersion != LocalIpcProtocol.CurrentVersion)
            {
                return AgentHealthResult.Failure(
                    HealthViewState.ProtocolMismatch,
                    "protocol_mismatch",
                    "The desktop and Agent protocol versions are not compatible.",
                    response.CorrelationId,
                    health.ProtocolVersion);
            }

            return AgentHealthResult.Connected(
                health.AgentStatus,
                health.IpcStatus,
                health.ProtocolVersion,
                health.HubConnectivityRequired,
                response.CorrelationId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AgentHealthResult.Failure(
                HealthViewState.TimedOut,
                "request_timeout",
                "The Agent did not respond before the health check timed out.",
                correlationId);
        }
        catch (LocalIpcProtocolMismatchException exception)
        {
            return AgentHealthResult.Failure(
                HealthViewState.ProtocolMismatch,
                "protocol_mismatch",
                "The desktop and Agent protocol versions are not compatible.",
                correlationId,
                exception.ActualVersion);
        }
        catch (LocalIpcProtocolException)
        {
            return AgentHealthResult.Failure(
                HealthViewState.InvalidResponse,
                "invalid_response",
                "The Agent returned an invalid health response.",
                correlationId);
        }
        catch (UnauthorizedAccessException)
        {
            return AgentHealthResult.Failure(
                HealthViewState.SecurityVerificationFailed,
                "security_verification_failed",
                "The local Agent connection could not be verified.",
                correlationId);
        }
        catch (SecurityException)
        {
            return AgentHealthResult.Failure(
                HealthViewState.SecurityVerificationFailed,
                "security_verification_failed",
                "The local Agent connection could not be verified.",
                correlationId);
        }
        catch (IOException)
        {
            return AgentHealthResult.Failure(
                HealthViewState.Unavailable,
                "agent_unavailable",
                "RMS Support Agent is not available on this machine.",
                correlationId);
        }
        catch (Exception)
        {
            return AgentHealthResult.Failure(
                HealthViewState.UnknownError,
                "health_check_failed",
                "The Agent health check could not be completed.",
                correlationId);
        }
    }

    private static AgentHealthResult MapAgentFailure(string? errorCode, string correlationId) =>
        errorCode?.Trim().ToLowerInvariant() switch
        {
            "protocol_mismatch" or "unsupported_protocol" => AgentHealthResult.Failure(
                HealthViewState.ProtocolMismatch,
                "protocol_mismatch",
                "The desktop and Agent protocol versions are not compatible.",
                correlationId),
            "security_verification_failed" or "unauthorized" => AgentHealthResult.Failure(
                HealthViewState.SecurityVerificationFailed,
                "security_verification_failed",
                "The local Agent connection could not be verified.",
                correlationId),
            "invalid_response" or "malformed_response" => AgentHealthResult.Failure(
                HealthViewState.InvalidResponse,
                "invalid_response",
                "The Agent returned an invalid health response.",
                correlationId),
            _ => AgentHealthResult.Failure(
                HealthViewState.Unavailable,
                "agent_unavailable",
                "RMS Support Agent is not available on this machine.",
                correlationId)
        };
}
