using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public sealed class LocalAgentLogEvidenceClient(LocalIpcClient localIpcClient)
    : ILocalLogEvidenceClient
{
    private static readonly IReadOnlyList<(string ServiceName, string DisplayName)> FixedServices =
    [
        ("RMS.BranchService", "RMS Branch Service"),
        ("RMS.CashierService", "RMS Cashier Service"),
        ("RMSServiceManager", "RMS Services Manager")
    ];

    public async Task<LogEvidenceResult> GetEvidenceAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await localIpcClient
                .GetLogEvidenceAsync(correlationId, cancellationToken)
                .ConfigureAwait(false);

            if (!response.Succeeded)
            {
                return MapFailure(response.ErrorCode, response.CorrelationId);
            }

            if (response.Result is not { } snapshot
                || !Enum.IsDefined(snapshot.OverallState)
                || snapshot.CheckedAtUtc == default
                || snapshot.Services is null
                || snapshot.Services.Count != FixedServices.Count)
            {
                return InvalidResponse(response.CorrelationId);
            }

            var rows = new List<LogEvidenceServiceRow>(FixedServices.Count);
            for (var index = 0; index < FixedServices.Count; index++)
            {
                var item = snapshot.Services[index];
                var expected = FixedServices[index];
                if (!string.Equals(item.ServiceId, ToServiceId(expected.ServiceName), StringComparison.Ordinal)
                    || !string.Equals(item.ServiceDisplayName, expected.DisplayName, StringComparison.Ordinal)
                    || !LogEvidenceServiceRow.TryCreate(item, out var row)
                    || row is null)
                {
                    return InvalidResponse(response.CorrelationId);
                }

                rows.Add(row);
            }

            var expectedState = DetermineOverallState(rows);
            var state = snapshot.OverallState switch
            {
                LogEvidenceOverallState.Healthy => LogEvidenceViewState.Healthy,
                LogEvidenceOverallState.Degraded => LogEvidenceViewState.Degraded,
                LogEvidenceOverallState.Unavailable => LogEvidenceViewState.Unavailable,
                LogEvidenceOverallState.Unknown => LogEvidenceViewState.Unknown,
                _ => LogEvidenceViewState.InvalidResponse
            };
            if (state == LogEvidenceViewState.InvalidResponse || state != expectedState)
            {
                return InvalidResponse(response.CorrelationId);
            }

            return LogEvidenceResult.Success(
                state,
                rows,
                snapshot.CheckedAtUtc,
                response.CorrelationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return LogEvidenceResult.Failure(
                LogEvidenceViewState.TimedOut,
                "logs_evidence_timeout",
                "RMS diagnostic evidence timed out.",
                correlationId);
        }
        catch (LocalIpcProtocolMismatchException)
        {
            return LogEvidenceResult.Failure(
                LogEvidenceViewState.ProtocolMismatch,
                "protocol_mismatch",
                "Desktop and Agent versions are not compatible.",
                correlationId);
        }
        catch (LocalIpcServerIdentityException)
        {
            return LogEvidenceResult.Failure(
                LogEvidenceViewState.SecurityVerificationFailed,
                "security_verification_failed",
                "The local Agent connection could not be verified.",
                correlationId);
        }
        catch (LocalIpcProtocolException)
        {
            return InvalidResponse(correlationId);
        }
        catch (UnauthorizedAccessException)
        {
            return SecurityFailure(correlationId);
        }
        catch (SecurityException)
        {
            return SecurityFailure(correlationId);
        }
        catch (IOException)
        {
            return LogEvidenceResult.Failure(
                LogEvidenceViewState.Unavailable,
                "agent_unavailable",
                "RMS Support Agent is not available on this machine.",
                correlationId);
        }
        catch (Exception)
        {
            return LogEvidenceResult.Failure(
                LogEvidenceViewState.UnknownError,
                "logs_evidence_failed",
                "RMS diagnostic evidence could not be determined.",
                correlationId);
        }
    }

    private static LogEvidenceResult MapFailure(string? errorCode, string correlationId) =>
        errorCode?.Trim().ToLowerInvariant() switch
        {
            "logs_evidence_timeout" or "request_timeout" => LogEvidenceResult.Failure(
                LogEvidenceViewState.TimedOut,
                "logs_evidence_timeout",
                "RMS diagnostic evidence timed out.",
                correlationId),
            "protocol_mismatch" or "unsupported_protocol_version" or "unknown_operation" => LogEvidenceResult.Failure(
                LogEvidenceViewState.ProtocolMismatch,
                "protocol_mismatch",
                "Desktop and Agent versions are not compatible.",
                correlationId),
            "security_verification_failed" or "unauthorized" or "diagnostic_authorization_required" => SecurityFailure(correlationId),
            "invalid_response" or "malformed_response" => InvalidResponse(correlationId),
            "logs_evidence_unavailable" => LogEvidenceResult.Failure(
                LogEvidenceViewState.Unavailable,
                "logs_evidence_unavailable",
                "RMS diagnostic evidence is currently unavailable.",
                correlationId),
            "agent_unavailable" => LogEvidenceResult.Failure(
                LogEvidenceViewState.Unavailable,
                "agent_unavailable",
                "RMS Support Agent is not available on this machine.",
                correlationId),
            _ => LogEvidenceResult.Failure(
                LogEvidenceViewState.UnknownError,
                "logs_evidence_failed",
                "RMS diagnostic evidence could not be determined.",
                correlationId)
        };

    private static LogEvidenceResult InvalidResponse(string correlationId) => LogEvidenceResult.Failure(
        LogEvidenceViewState.InvalidResponse,
        "invalid_response",
        "The Agent returned an invalid logs and evidence response.",
        correlationId);

    private static LogEvidenceResult SecurityFailure(string correlationId) => LogEvidenceResult.Failure(
        LogEvidenceViewState.SecurityVerificationFailed,
        "security_verification_failed",
        "The local Agent connection could not be verified.",
        correlationId);

    private static LogEvidenceViewState DetermineOverallState(
        IReadOnlyList<LogEvidenceServiceRow> rows)
    {
        if (rows.All(row => row.Category == FailureCategory.None && row.UnknownReasons.Count == 0))
        {
            return LogEvidenceViewState.Healthy;
        }

        if (rows.All(row => row.Category == FailureCategory.Unknown && row.Records.Count == 0))
        {
            return LogEvidenceViewState.Unavailable;
        }

        return LogEvidenceViewState.Degraded;
    }

    private static string ToServiceId(string serviceName) =>
        "svc-" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(serviceName.Trim())))
            .ToLowerInvariant()[..16];
}
