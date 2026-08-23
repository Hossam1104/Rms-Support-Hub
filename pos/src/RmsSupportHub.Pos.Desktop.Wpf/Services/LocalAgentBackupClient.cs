using System.IO;
using System.Security;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public sealed class LocalAgentBackupClient(LocalIpcClient localIpcClient) : ILocalBackupClient
{
    private const long MaximumBackupBytes = 512L * 1024 * 1024;

    public async Task<BackupInventoryResult> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        try
        {
            var authority = await localIpcClient.GetAuthorizationAsync(correlationId, cancellationToken).ConfigureAwait(false);
            if (!authority.Succeeded || authority.Result is not { } auth || !IsValidAuthority(auth))
            {
                return MapInventoryFailure(authority.ErrorCode ?? "invalid_response", correlationId);
            }

            if (!auth.CanReadBackupInventory)
            {
                return BackupInventoryResult.Failure(
                    BackupViewState.Unauthorized,
                    "diagnostic_authorization_required",
                    "An authorized local caller is required to view backup inventory.");
            }

            var response = await localIpcClient.GetBackupInventoryAsync(correlationId, cancellationToken).ConfigureAwait(false);
            if (!response.Succeeded || response.Result is not { } inventory
                || inventory.CheckedAtUtc == default
                || inventory.Items is null
                || inventory.Items.Count > 64)
            {
                return MapInventoryFailure(response.ErrorCode ?? "invalid_response", correlationId);
            }

            var rows = new List<BackupArtifactRow>(inventory.Items.Count);
            foreach (var item in inventory.Items)
            {
                if (!BackupArtifactRow.TryCreate(item, out var row) || row is null)
                {
                    return BackupInventoryResult.Failure(
                        BackupViewState.InvalidResponse,
                        "invalid_response",
                        "The Agent returned an invalid backup inventory response.");
                }

                rows.Add(row);
            }

            return new(
                BackupViewState.Succeeded,
                rows.OrderByDescending(row => row.CreatedAtUtc).ToArray(),
                inventory.CheckedAtUtc,
                string.Empty,
                string.Empty,
                true,
                auth.CanCreateBackup,
                auth.CanExportArtifacts);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return BackupInventoryResult.Failure(BackupViewState.Cancelled, "cancelled", "Backup inventory was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return BackupInventoryResult.Failure(BackupViewState.TimedOut, "backup_inventory_timeout", "Backup inventory timed out.");
        }
        catch (LocalIpcProtocolMismatchException)
        {
            return BackupInventoryResult.Failure(BackupViewState.ProtocolMismatch, "protocol_mismatch", "Desktop and Agent versions are not compatible.");
        }
        catch (LocalIpcServerIdentityException)
        {
            return BackupInventoryResult.Failure(BackupViewState.SecurityVerificationFailed, "security_verification_failed", "The local Agent connection could not be verified.");
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException)
        {
            return BackupInventoryResult.Failure(BackupViewState.SecurityVerificationFailed, "security_verification_failed", "The local Agent connection could not be verified.");
        }
        catch (LocalIpcProtocolException)
        {
            return BackupInventoryResult.Failure(BackupViewState.InvalidResponse, "invalid_response", "The Agent returned an invalid backup response.");
        }
        catch (IOException)
        {
            return BackupInventoryResult.Failure(BackupViewState.Unavailable, "agent_unavailable", "RMS Support Agent is not available on this machine.");
        }
        catch
        {
            return BackupInventoryResult.Failure(BackupViewState.Failed, "backup_inventory_failed", "Backup inventory could not be determined.");
        }
    }

    public async Task<BackupOperationResult> CreateAsync(
        RmsDatabaseTarget target,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        string? operationId = null;
        try
        {
            var response = await localIpcClient.CreateBackupAsync(target, correlationId, cancellationToken).ConfigureAwait(false);
            if (!response.Succeeded || response.Result is not { } operation)
            {
                return MapOperationFailure(response.ErrorCode, response.ErrorMessage);
            }

            if (!IsValidOperation(operation, target, correlationId))
            {
                return BackupOperationResult.Failure(BackupViewState.InvalidResponse, "invalid_response", "The Agent returned an invalid backup operation response.");
            }

            operationId = operation.OperationId;
            while (operation.State is RmsDatabaseOperationState.Accepted or RmsDatabaseOperationState.Running)
            {
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                var status = await localIpcClient.GetBackupStatusAsync(target, operation.OperationId, correlationId, cancellationToken).ConfigureAwait(false);
                if (!status.Succeeded || status.Result is not { } current || !IsValidOperation(current, target, correlationId))
                {
                    return BackupOperationResult.Failure(BackupViewState.InvalidResponse, "invalid_response", "The Agent returned an invalid backup status response.");
                }

                operation = current;
            }

            return new(MapOperationState(operation), operation, operation.ErrorCode ?? string.Empty, operation.Detail);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TryCancelAsync(target, operationId, correlationId).ConfigureAwait(false);
            return BackupOperationResult.Failure(BackupViewState.Cancelled, "cancelled", "The backup was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return BackupOperationResult.Failure(BackupViewState.TimedOut, "backup_timeout", "The backup timed out.");
        }
        catch (LocalIpcProtocolMismatchException)
        {
            return BackupOperationResult.Failure(BackupViewState.ProtocolMismatch, "protocol_mismatch", "Desktop and Agent versions are not compatible.");
        }
        catch (LocalIpcServerIdentityException)
        {
            return BackupOperationResult.Failure(BackupViewState.SecurityVerificationFailed, "security_verification_failed", "The local Agent connection could not be verified.");
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException)
        {
            return BackupOperationResult.Failure(BackupViewState.SecurityVerificationFailed, "security_verification_failed", "The local Agent connection could not be verified.");
        }
        catch (LocalIpcProtocolException)
        {
            return BackupOperationResult.Failure(BackupViewState.InvalidResponse, "invalid_response", "The Agent returned an invalid backup response.");
        }
        catch (IOException)
        {
            return BackupOperationResult.Failure(BackupViewState.Unavailable, "agent_unavailable", "RMS Support Agent is not available on this machine.");
        }
        catch
        {
            return BackupOperationResult.Failure(BackupViewState.Failed, "backup_failed", "The RMS database backup could not be completed.");
        }
    }

    private async Task TryCancelAsync(
        RmsDatabaseTarget target,
        string? operationId,
        string correlationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return;
        }

        try
        {
            await localIpcClient
                .CancelBackupAsync(target, operationId, correlationId, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch
        {
            // The caller is already cancelling. The Agent owns the operation state and
            // will revoke any completed artifact even if this best-effort IPC call fails.
        }
    }

    public async Task<ArtifactExportResult> ExportAsync(
        LocalArtifactExportRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await localIpcClient.ExportArtifactAsync(
                new(
                    request.ArtifactKind,
                    request.ArtifactId,
                    request.DatabaseTarget,
                    request.DestinationPath,
                    request.OverwriteConfirmed),
                Guid.NewGuid().ToString("N"),
                cancellationToken).ConfigureAwait(false);
            if (!response.Succeeded || response.Result is not { } result || !IsValidExport(result, request.ArtifactId))
            {
                return ArtifactExportResult.Failure(ArtifactExportViewState.Failed, response.ErrorCode ?? "invalid_response", "The artifact export could not be completed.", request.ArtifactId);
            }

            return MapExport(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ArtifactExportResult.Failure(ArtifactExportViewState.Cancelled, "cancelled", "Artifact export was cancelled.", request.ArtifactId);
        }
        catch (LocalIpcProtocolMismatchException)
        {
            return ArtifactExportResult.Failure(ArtifactExportViewState.Failed, "protocol_mismatch", "Desktop and Agent versions are not compatible.", request.ArtifactId);
        }
        catch (LocalIpcServerIdentityException)
        {
            return ArtifactExportResult.Failure(ArtifactExportViewState.Failed, "security_verification_failed", "The local Agent connection could not be verified.", request.ArtifactId);
        }
        catch (LocalIpcProtocolException)
        {
            return ArtifactExportResult.Failure(ArtifactExportViewState.Failed, "invalid_response", "The Agent returned an invalid export response.", request.ArtifactId);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException)
        {
            return ArtifactExportResult.Failure(ArtifactExportViewState.Unauthorized, "unauthorized", "Administrator authority is required to export artifacts.", request.ArtifactId);
        }
        catch (IOException)
        {
            return ArtifactExportResult.Failure(ArtifactExportViewState.Failed, "agent_unavailable", "RMS Support Agent is not available on this machine.", request.ArtifactId);
        }
        catch
        {
            return ArtifactExportResult.Failure(ArtifactExportViewState.Failed, "export_failed", "The artifact export could not be completed.", request.ArtifactId);
        }
    }

    private static BackupViewState MapOperationState(RmsDatabaseOperationDto operation) => operation.ErrorCode switch
    {
        "cancelled" => BackupViewState.Cancelled,
        "audit_unavailable" => BackupViewState.AuditUnavailable,
        "operation_in_progress" => BackupViewState.OperationInProgress,
        _ when operation.Outcome == RmsDatabaseOperationOutcome.Completed => BackupViewState.Succeeded,
        _ when operation.ErrorCode is "database_configuration_invalid" or "database_unavailable" or "database_unreachable" => BackupViewState.Unavailable,
        _ => operation.Outcome == RmsDatabaseOperationOutcome.Failed ? BackupViewState.Failed : BackupViewState.InvalidResponse
    };

    private static BackupOperationResult MapOperationFailure(string? code, string? detail) =>
        code?.Trim().ToLowerInvariant() switch
        {
            "administrator_authorization_required" => BackupOperationResult.Failure(BackupViewState.Unauthorized, code!, "Administrator authority is required to create backups."),
            "operation_in_progress" => BackupOperationResult.Failure(BackupViewState.OperationInProgress, code!, "Another backup for this database is already in progress."),
            "agent_unavailable" => BackupOperationResult.Failure(BackupViewState.Unavailable, code!, "RMS Support Agent is not available on this machine."),
            "request_timeout" => BackupOperationResult.Failure(BackupViewState.TimedOut, code!, "The backup request timed out."),
            _ => BackupOperationResult.Failure(BackupViewState.Failed, code ?? "backup_failed", detail ?? "The backup could not be started.")
        };

    private static BackupViewState MapInventoryError(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "administrator_authorization_required" or "diagnostic_authorization_required" => BackupViewState.Unauthorized,
        "protocol_mismatch" or "unsupported_protocol_version" or "unknown_operation" => BackupViewState.ProtocolMismatch,
        "agent_unavailable" => BackupViewState.Unavailable,
        _ => BackupViewState.Failed
    };

    private static BackupInventoryResult MapInventoryFailure(string code, string correlationId) =>
        BackupInventoryResult.Failure(MapInventoryError(code), code, "Backup inventory could not be determined.");

    private static ArtifactExportResult MapExport(LocalIpcArtifactExportResultDto result)
    {
        var state = result.State switch
        {
            LocalIpcArtifactExportState.Succeeded => ArtifactExportViewState.Succeeded,
            LocalIpcArtifactExportState.DestinationExists => ArtifactExportViewState.DestinationExists,
            LocalIpcArtifactExportState.DestinationRejected => ArtifactExportViewState.DestinationRejected,
            LocalIpcArtifactExportState.ArtifactExpired => ArtifactExportViewState.ArtifactExpired,
            LocalIpcArtifactExportState.ArtifactNotFound => ArtifactExportViewState.ArtifactNotFound,
            LocalIpcArtifactExportState.ChecksumMismatch => ArtifactExportViewState.ChecksumMismatch,
            LocalIpcArtifactExportState.Unauthorized => ArtifactExportViewState.Unauthorized,
            LocalIpcArtifactExportState.Cancelled => ArtifactExportViewState.Cancelled,
            _ => ArtifactExportViewState.Failed
        };
        return new(state, result.ArtifactId, result.DisplayName, result.SizeBytes, result.Sha256Checksum, result.DestinationCategory, result.ErrorCode ?? string.Empty, state == ArtifactExportViewState.Succeeded ? string.Empty : "The artifact export was not completed.");
    }

    private static bool IsValidAuthority(LocalIpcAuthorizationDto value) =>
        value.AuthorizationLevel is "LocalOperator" or "LocalAdministrator" or "Unauthenticated"
        && (!value.CanCreateBackup || value.CanReadBackupInventory)
        && (!value.CanExportArtifacts || value.CanCreateBackup);

    private static bool IsValidOperation(RmsDatabaseOperationDto value, RmsDatabaseTarget target, string correlationId) =>
        value.OperationId.Length == 32
        && value.OperationId.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')
        && value.Target == target
        && value.Operation == RmsDatabaseOperationKind.Backup
        && value.CorrelationId == correlationId
        && Enum.IsDefined(value.State)
        && Enum.IsDefined(value.Outcome)
        && value.ProgressPercent is >= 0 and <= 100
        && value.Detail.Length <= 512
        && !value.Detail.Any(char.IsControl)
        && (value.Artifact is null || IsValidArtifact(value.Artifact));

    private static bool IsValidArtifact(RmsDatabaseArtifactDto artifact) =>
        artifact.ArtifactId.Length == 32
        && artifact.ArtifactId.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')
        && artifact.SizeBytes > 0
        && artifact.SizeBytes <= MaximumBackupBytes
        && artifact.Sha256Checksum.Length == 64
        && artifact.Sha256Checksum.All(Uri.IsHexDigit);

    private static bool IsValidExport(LocalIpcArtifactExportResultDto value, string artifactId) =>
        Enum.IsDefined(value.State)
        && value.ArtifactId == artifactId
        && value.DisplayName.Length <= 128
        && !value.DisplayName.Any(char.IsControl)
        && value.SizeBytes >= 0
        && value.Sha256Checksum.Length <= 64
        && value.DestinationCategory.Length <= 32;
}
