using System.IO;
using System.Security;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public sealed class LocalAgentDatabaseHealthClient(LocalIpcClient localIpcClient)
    : ILocalDatabaseHealthClient
{
    public async Task<DatabaseHealthResult> GetHealthAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await localIpcClient
                .GetDatabaseHealthAsync(correlationId, cancellationToken)
                .ConfigureAwait(false);

            if (!response.Succeeded)
            {
                return MapFailure(response.ErrorCode, response.CorrelationId);
            }

            if (response.Result is not { } snapshot
                || !Enum.IsDefined(snapshot.OverallState)
                || snapshot.Databases is null
                || snapshot.Databases.Count != 2)
            {
                return InvalidResponse(response.CorrelationId);
            }

            var rows = new List<DatabaseHealthRow>(snapshot.Databases.Count);
            foreach (var item in snapshot.Databases)
            {
                if (!DatabaseHealthRow.TryCreate(item, out var row) || row is null)
                {
                    return InvalidResponse(response.CorrelationId);
                }

                rows.Add(row);
            }

            if (rows.Select(row => row.DatabaseKind).Distinct().Count() != 2
                || MapOverallState(rows) != snapshot.OverallState)
            {
                return InvalidResponse(response.CorrelationId);
            }

            var state = snapshot.OverallState switch
            {
                RmsDatabaseHealthOverallState.Healthy => DatabaseHealthViewState.Healthy,
                RmsDatabaseHealthOverallState.Degraded => DatabaseHealthViewState.Degraded,
                RmsDatabaseHealthOverallState.Unavailable => DatabaseHealthViewState.Unavailable,
                RmsDatabaseHealthOverallState.Unknown => DatabaseHealthViewState.Unknown,
                _ => DatabaseHealthViewState.InvalidResponse
            };
            return state == DatabaseHealthViewState.InvalidResponse
                ? InvalidResponse(response.CorrelationId)
                : DatabaseHealthResult.Success(
                    state,
                    rows.OrderBy(row => row.DatabaseKind).ToArray(),
                    snapshot.CheckedAtUtc,
                    response.CorrelationId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DatabaseHealthResult.Failure(
                DatabaseHealthViewState.TimedOut,
                "database_health_timeout",
                "Database health check timed out.",
                correlationId);
        }
        catch (LocalIpcProtocolMismatchException)
        {
            return DatabaseHealthResult.Failure(
                DatabaseHealthViewState.ProtocolMismatch,
                "protocol_mismatch",
                "Desktop and Agent versions are not compatible.",
                correlationId);
        }
        catch (LocalIpcServerIdentityException)
        {
            return DatabaseHealthResult.Failure(
                DatabaseHealthViewState.SecurityVerificationFailed,
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
            return DatabaseHealthResult.Failure(
                DatabaseHealthViewState.Unavailable,
                "agent_unavailable",
                "RMS Support Agent is not available on this machine.",
                correlationId);
        }
        catch (Exception)
        {
            return DatabaseHealthResult.Failure(
                DatabaseHealthViewState.UnknownError,
                "database_health_failed",
                "Database health could not be determined.",
                correlationId);
        }
    }

    private static DatabaseHealthResult MapFailure(string? errorCode, string correlationId) =>
        errorCode?.Trim().ToLowerInvariant() switch
        {
            "database_health_timeout" or "request_timeout" => DatabaseHealthResult.Failure(
                DatabaseHealthViewState.TimedOut,
                "database_health_timeout",
                "Database health check timed out.",
                correlationId),
            "protocol_mismatch" or "unsupported_protocol_version" or "unknown_operation" => DatabaseHealthResult.Failure(
                DatabaseHealthViewState.ProtocolMismatch,
                "protocol_mismatch",
                "Desktop and Agent versions are not compatible.",
                correlationId),
            "security_verification_failed" or "unauthorized" => SecurityFailure(correlationId),
            "invalid_response" or "malformed_response" => InvalidResponse(correlationId),
            "database_health_unavailable" => DatabaseHealthResult.Failure(
                DatabaseHealthViewState.Unavailable,
                "database_health_unavailable",
                "RMS database health is currently unavailable.",
                correlationId),
            "agent_unavailable" => DatabaseHealthResult.Failure(
                DatabaseHealthViewState.Unavailable,
                "agent_unavailable",
                "RMS Support Agent is not available on this machine.",
                correlationId),
            _ => DatabaseHealthResult.Failure(
                DatabaseHealthViewState.UnknownError,
                "database_health_failed",
                "Database health could not be determined.",
                correlationId)
        };

    private static DatabaseHealthResult InvalidResponse(string correlationId) => DatabaseHealthResult.Failure(
        DatabaseHealthViewState.InvalidResponse,
        "invalid_response",
        "The Agent returned an invalid database health response.",
        correlationId);

    private static DatabaseHealthResult SecurityFailure(string correlationId) => DatabaseHealthResult.Failure(
        DatabaseHealthViewState.SecurityVerificationFailed,
        "security_verification_failed",
        "The local Agent connection could not be verified.",
        correlationId);

    private static RmsDatabaseHealthOverallState MapOverallState(
        IReadOnlyList<DatabaseHealthRow> rows)
    {
        if (rows.All(row => row.Status == RmsDatabaseDiagnosticStatus.Reachable))
        {
            return RmsDatabaseHealthOverallState.Healthy;
        }

        if (rows.All(row => row.Status is
            RmsDatabaseDiagnosticStatus.AuthenticationFailed
                or RmsDatabaseDiagnosticStatus.DatabaseUnavailable
                or RmsDatabaseDiagnosticStatus.Unreachable))
        {
            return RmsDatabaseHealthOverallState.Unavailable;
        }

        return RmsDatabaseHealthOverallState.Degraded;
    }
}
