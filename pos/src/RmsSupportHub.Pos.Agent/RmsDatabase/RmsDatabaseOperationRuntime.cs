using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.RmsDatabase;

public static class RmsDatabaseOperation
{
    public const string BackupOperationId = "rms.database.backup";
    public const string RestoreOperationId = "rms.database.restore";
    public const string HttpMethod = "POST";
    public const string BackupHttpPathTemplate = "/api/v1/rms/databases/{targetId}/backup";
    public const string RestoreHttpPathTemplate = "/api/v1/rms/databases/{targetId}/restore";

    public static MutationOperationDescriptor BackupDescriptor { get; } = new(
        BackupOperationId,
        HttpMethod,
        BackupHttpPathTemplate,
        MutationTargetKind.AllowListedRmsDatabase);

    public static MutationOperationDescriptor RestoreDescriptor { get; } = new(
        RestoreOperationId,
        HttpMethod,
        RestoreHttpPathTemplate,
        MutationTargetKind.AllowListedRmsDatabase);
}

public enum RmsDatabaseSecurityFailure
{
    None,
    PrincipalUnavailable,
    MutationTokenInvalid
}

public sealed record RmsDatabaseExecutionResult(
    RmsDatabaseOperationDto? Operation,
    RmsDatabaseSecurityFailure SecurityFailure = RmsDatabaseSecurityFailure.None,
    int? HttpStatus = null);

/// <summary>
/// Agent boundary for typed RMS database mutations. It owns target/path resolution, token binding,
/// idempotency, concurrency, progress state, and privileged audit; the application workflow owns
/// database/service policy and the infrastructure adapter owns native SQL.
/// </summary>
public sealed class RmsDatabaseOperationRuntime(
    IRmsDatabaseWorkflow workflow,
    IRmsDatabaseDiagnostics diagnostics,
    IRmsDatabaseBackupStorage storage,
    RmsDatabaseOperationStore operations,
    RmsDatabaseIdempotencyStore idempotency,
    RmsDatabaseConcurrencyGate concurrency,
    IMutationTokenStore mutationTokens,
    IAgentPrincipalSidResolver principalSidResolver,
    IRmsPrivilegedAuditSink audit,
    TimeProvider clock)
{
    public Task<RmsDatabaseExecutionResult> ExecuteBackupAsync(
        HttpContext context,
        string? targetId,
        RmsDatabaseBackupRequestDto? request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            context,
            targetId,
            RmsDatabaseOperationKind.Backup,
            request?.IdempotencyKey,
            null,
            null,
            cancellationToken);

    public Task<RmsDatabaseExecutionResult> ExecuteRestoreAsync(
        HttpContext context,
        string? targetId,
        RmsDatabaseRestoreRequestDto? request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            context,
            targetId,
            RmsDatabaseOperationKind.Restore,
            request?.IdempotencyKey,
            request?.BackupArtifactId,
            request?.ConfirmationText,
            cancellationToken);

    public bool TryGet(
        string principalSid,
        string? targetId,
        string? operationId,
        out RmsDatabaseOperationDto? operation)
    {
        if (!RmsDatabaseCatalog.TryResolve(targetId, out var definition)
            || string.IsNullOrWhiteSpace(operationId))
        {
            operation = null;
            return false;
        }

        return operations.TryGet(principalSid, definition.Kind, operationId, out operation);
    }

    public async Task<RmsDatabaseWorkspaceDto?> GetWorkspaceAsync(
        string principalSid,
        string? targetId,
        CancellationToken cancellationToken = default)
    {
        if (!RmsDatabaseCatalog.TryResolve(targetId, out var definition))
        {
            return null;
        }

        var backups = await storage.ListAsync(definition.Kind, cancellationToken).ConfigureAwait(false);
        return new(
            ToContractTarget(definition.Kind),
            definition.DisplayName,
            $"RESTORE {definition.DisplayName.ToUpperInvariant()}",
            backups.Select(ToArtifact).ToArray(),
            operations.GetLatest(principalSid, definition.Kind));
    }

    public IAsyncEnumerable<RmsDatabaseOperationDto> Stream(
        string principalSid,
        string? targetId,
        string? operationId,
        CancellationToken cancellationToken = default)
    {
        return RmsDatabaseCatalog.TryResolve(targetId, out var definition) && !string.IsNullOrWhiteSpace(operationId)
            ? operations.StreamAsync(principalSid, definition.Kind, operationId!, cancellationToken)
            : AsyncEnumerableEmpty();
    }

    private async Task<RmsDatabaseExecutionResult> ExecuteAsync(
        HttpContext context,
        string? targetId,
        RmsDatabaseOperationKind operation,
        string? idempotencyKey,
        string? artifactId,
        string? confirmationText,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var correlationId = CorrelationIdContext.TryGet(context) ?? "unavailable";
        if (!RmsDatabaseCatalog.TryResolve(targetId, out var definition))
        {
            return Rejected(null, RmsDatabaseOperationKind.Backup, correlationId, "database_target_invalid", "The requested RMS database target is not supported.");
        }

        if (operation == RmsDatabaseOperationKind.Restore
            && !string.Equals(
                confirmationText,
                $"RESTORE {definition.DisplayName.ToUpperInvariant()}",
                StringComparison.Ordinal))
        {
            return Rejected(definition, operation, correlationId, "restore_confirmation_invalid", "The destructive restore confirmation did not match the selected database.");
        }

        if (!RmsDatabaseIdempotencyStore.IsValidKey(idempotencyKey))
        {
            return Rejected(definition, operation, correlationId, "database_idempotency_invalid", "A bounded idempotency key is required.");
        }

        if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
        {
            return new(null, RmsDatabaseSecurityFailure.PrincipalUnavailable);
        }

        Audit(new(
            clock.GetUtcNow(),
            RmsPrivilegedAuditEventKind.Requested,
            definition.Kind,
            OperationName(operation),
            correlationId,
            principalSid,
            "A typed RMS database operation was requested."));

        var reservation = idempotency.TryReserve(
            principalSid,
            definition.Kind,
            operation,
            idempotencyKey!);
        if (reservation.State is RmsDatabaseReservationState.Completed && reservation.OperationId is not null)
        {
            return operations.TryGet(principalSid, definition.Kind, reservation.OperationId, out var existing)
                ? new(existing)
                : Rejected(definition, operation, correlationId, "database_operation_state_expired", "The previous operation state is no longer retained.");
        }

        if (reservation.State == RmsDatabaseReservationState.InProgress)
        {
            return Rejected(definition, operation, correlationId, "database_operation_in_progress", "An identical database operation is already in progress.");
        }

        if (reservation.State == RmsDatabaseReservationState.Conflict)
        {
            return Rejected(definition, operation, correlationId, "database_idempotency_conflict", "The idempotency key is already bound to another database operation.");
        }

        if (reservation.State == RmsDatabaseReservationState.Capacity)
        {
            return Rejected(definition, operation, correlationId, "database_idempotency_capacity", "The Agent cannot retain another database operation at this time.");
        }

        var reservationHeld = true;
        RmsDatabaseLease? lease = null;
        try
        {
            var preflight = await PreflightAsync(definition.Kind, operation, artifactId, cancellationToken)
                .ConfigureAwait(false);
            if (preflight is not null)
            {
                idempotency.Release(principalSid, definition.Kind, operation, idempotencyKey!);
                reservationHeld = false;
                return Rejected(definition, operation, correlationId, preflight.Value.Code, preflight.Value.Detail);
            }

            if (!TryConsumeMutationToken(context, definition, operation, principalSid, out var tokenFailure))
            {
                idempotency.Release(principalSid, definition.Kind, operation, idempotencyKey!);
                reservationHeld = false;
                return new(null, tokenFailure);
            }

            lease = concurrency.TryEnter(definition.Kind);
            if (lease is null)
            {
                idempotency.Release(principalSid, definition.Kind, operation, idempotencyKey!);
                reservationHeld = false;
                return Rejected(definition, operation, correlationId, "database_operation_busy", "Another database operation for this target is already in progress.");
            }

            RmsDatabaseOperationHandle handle;
            try
            {
                handle = operations.Create(principalSid, definition.Kind, operation, correlationId);
            }
            catch (RmsDatabaseOperationCapacityException)
            {
                idempotency.Release(principalSid, definition.Kind, operation, idempotencyKey!);
                reservationHeld = false;
                lease.Dispose();
                return Rejected(definition, operation, correlationId, "database_operation_capacity", "The Agent cannot retain another database operation at this time.");
            }

            idempotency.BindOperation(principalSid, definition.Kind, operation, idempotencyKey!, handle.OperationId);
            reservationHeld = false;
            operations.Start(handle.OperationId);
            Audit(new(
                clock.GetUtcNow(),
                RmsPrivilegedAuditEventKind.Accepted,
                definition.Kind,
                OperationName(operation),
                correlationId,
                principalSid,
                "The typed database operation passed preflight and was accepted."));

            _ = Task.Run(
                () => RunAsync(
                    handle,
                    principalSid,
                    definition.Kind,
                    operation,
                    artifactId,
                    correlationId,
                    lease!),
                CancellationToken.None);

            return new(handle.InitialState with
            {
                State = RmsDatabaseOperationState.Running,
                Detail = "The Agent is running the server-owned database operation.",
                ProgressPercent = 1,
                Stage = "running"
            });
        }
        finally
        {
            if (reservationHeld)
            {
                idempotency.Release(principalSid, definition.Kind, operation, idempotencyKey!);
            }
        }
    }

    private async Task RunAsync(
        RmsDatabaseOperationHandle handle,
        string principalSid,
        RmsDatabaseKind database,
        RmsDatabaseOperationKind operation,
        string? artifactId,
        string correlationId,
        RmsDatabaseLease lease)
    {
        try
        {
            Audit(new(
                clock.GetUtcNow(),
                RmsPrivilegedAuditEventKind.Started,
                database,
                OperationName(operation),
                correlationId,
                principalSid,
                "The typed RMS database operation started."));

            var progress = new Progress<RmsDatabaseProgress>(update =>
            {
                operations.Progress(handle.OperationId, update);
                if (update.Stage is "service-stop" or "service-recovery")
                {
                    Audit(new(
                        clock.GetUtcNow(),
                        RmsPrivilegedAuditEventKind.ServiceCoordination,
                        database,
                        OperationName(operation),
                        correlationId,
                        principalSid,
                        update.Detail));
                }
                else if (update.Stage == "dispatch")
                {
                    Audit(new(
                        clock.GetUtcNow(),
                        RmsPrivilegedAuditEventKind.Dispatch,
                        database,
                        OperationName(operation),
                        correlationId,
                        principalSid,
                        update.Detail));
                }
            });
            var result = operation == RmsDatabaseOperationKind.Backup
                ? await workflow.BackupAsync(database, progress, CancellationToken.None).ConfigureAwait(false)
                : await workflow.RestoreAsync(database, artifactId!, progress, CancellationToken.None).ConfigureAwait(false);

            operations.Complete(
                handle.OperationId,
                result.Outcome,
                result.Code,
                result.Detail,
                result.Backup,
                result.DestructiveAttempted,
                result.RecoveryRequired,
                result.Warnings);
            Audit(new(
                clock.GetUtcNow(),
                ToAuditKind(result.Outcome),
                database,
                OperationName(operation),
                correlationId,
                principalSid,
                result.Detail));
        }
        catch
        {
            operations.Complete(
                handle.OperationId,
                RmsDatabaseWorkflowOutcome.OutcomeUnknown,
                "database_operation_outcome_unknown",
                "The database operation failed ambiguously. Check database and service state before any retry.",
                null,
                operation == RmsDatabaseOperationKind.Restore,
                true,
                []);
            Audit(new(
                clock.GetUtcNow(),
                RmsPrivilegedAuditEventKind.OutcomeUnknown,
                database,
                OperationName(operation),
                correlationId,
                principalSid,
                "The database operation outcome is unknown."));
        }
        finally
        {
            lease.Dispose();
        }
    }

    private async Task<(string Code, string Detail)?> PreflightAsync(
        RmsDatabaseKind database,
        RmsDatabaseOperationKind operation,
        string? artifactId,
        CancellationToken cancellationToken)
    {
        if (operation == RmsDatabaseOperationKind.Restore
            && await storage.ResolveAsync(database, artifactId ?? string.Empty, cancellationToken).ConfigureAwait(false) is null)
        {
            return ("restore_backup_not_approved", "The selected backup is not an approved Agent-owned artifact.");
        }

        // Restore must remain possible when the target database itself cannot open, so it is
        // preflighted against SQL Server/master connectivity only, never the target database.
        if (operation == RmsDatabaseOperationKind.Restore)
        {
            var serverDiagnostic = await diagnostics.DiagnoseServerAsync(database, cancellationToken).ConfigureAwait(false);
            if (serverDiagnostic.Status == RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus.Reachable)
            {
                return null;
            }

            return serverDiagnostic.Status switch
            {
                RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus.DatabaseNameMismatch => ("database_name_mismatch", "The installed RMS database identity does not match the canonical target; no database operation was attempted."),
                RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus.AuthenticationFailed => ("restore_server_authentication_failed", "SQL Server authentication failed for the installed RMS database configuration; no database operation was attempted."),
                RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus.NotConfigured or RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus.ConfigurationInvalid => ("restore_server_configuration_invalid", "The installed RMS database configuration is unavailable or invalid; no database operation was attempted."),
                _ => ("restore_server_unreachable", "The SQL Server hosting the installed RMS database could not be reached; no database operation was attempted.")
            };
        }

        var diagnostic = await diagnostics.DiagnoseAsync(database, cancellationToken).ConfigureAwait(false);
        if (diagnostic.Status == RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus.Reachable && diagnostic.DatabaseNameMatches == true)
        {
            return null;
        }

        return diagnostic.Status switch
        {
            RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus.DatabaseNameMismatch => ("database_name_mismatch", "The installed RMS database identity does not match the canonical target; no database operation was attempted."),
            RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus.AuthenticationFailed => ("database_authentication_failed", "The installed RMS database authentication failed; no database operation was attempted."),
            RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus.DatabaseUnavailable => ("database_unavailable", "The installed RMS database is unavailable; no database operation was attempted."),
            RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus.NotConfigured or RmsSupportHub.Pos.Domain.Interfaces.RmsDatabaseDiagnosticStatus.ConfigurationInvalid => ("database_configuration_invalid", "The installed RMS database configuration is unavailable or invalid; no database operation was attempted."),
            _ => ("database_unreachable", "The installed RMS database could not be reached; no database operation was attempted.")
        };
    }

    private bool TryConsumeMutationToken(
        HttpContext context,
        RmsDatabaseTargetDefinition definition,
        RmsDatabaseOperationKind operation,
        string principalSid,
        out RmsDatabaseSecurityFailure failure)
    {
        failure = RmsDatabaseSecurityFailure.None;
        var tokenValues = context.Request.Headers[MutationTokenContract.HeaderName];
        var originValues = context.Request.Headers.Origin;
        if (tokenValues.Count != 1 || originValues.Count != 1 || string.IsNullOrWhiteSpace(tokenValues[0]) || string.IsNullOrWhiteSpace(originValues[0]))
        {
            failure = RmsDatabaseSecurityFailure.MutationTokenInvalid;
            return false;
        }

        var descriptor = operation == RmsDatabaseOperationKind.Backup
            ? RmsDatabaseOperation.BackupDescriptor
            : RmsDatabaseOperation.RestoreDescriptor;
        if (!descriptor.TryResolveHttpPath(definition.TargetId, out var expectedPath)
            || !string.Equals(context.Request.Method, RmsDatabaseOperation.HttpMethod, StringComparison.Ordinal)
            || !string.Equals(context.Request.Path.Value, expectedPath, StringComparison.Ordinal))
        {
            failure = RmsDatabaseSecurityFailure.MutationTokenInvalid;
            return false;
        }

        var consumed = mutationTokens.TryConsume(new MutationTokenValidationRequest(
            tokenValues[0]!,
            principalSid,
            originValues[0]!,
            context.Request.Method,
            descriptor.OperationId,
            context.Request.Path.Value));
        if (!consumed.Succeeded)
        {
            failure = RmsDatabaseSecurityFailure.MutationTokenInvalid;
            return false;
        }

        Audit(new(
            clock.GetUtcNow(),
            RmsPrivilegedAuditEventKind.Dispatch,
            definition.Kind,
            OperationName(operation),
            CorrelationIdContext.TryGet(context) ?? "unavailable",
            principalSid,
            "The one-use mutation token was consumed immediately before operation dispatch."));
        return true;
    }

    private void Audit(RmsPrivilegedAuditEvent auditEvent)
    {
        try
        {
            audit.Record(auditEvent);
        }
        catch
        {
            // Audit failure must not turn a completed database operation into a second dispatch.
        }
    }

    private static RmsDatabaseExecutionResult Rejected(
        RmsDatabaseTargetDefinition? definition,
        RmsDatabaseOperationKind operationKind,
        string correlationId,
        string code,
        string detail)
    {
        if (definition is null)
        {
            return new(null, RmsDatabaseSecurityFailure.None, StatusCodes.Status400BadRequest);
        }

        var now = DateTimeOffset.UtcNow;
        var operation = new RmsDatabaseOperationDto(
            Guid.NewGuid().ToString("N"),
            ToContractTarget(definition.Kind),
            definition.DisplayName,
            operationKind,
            RmsDatabaseOperationState.NotAttempted,
            RmsDatabaseOperationOutcome.NotAttempted,
            0,
            "not-attempted",
            detail,
            now,
            now,
            null,
            false,
            false,
            [],
            code,
            correlationId);
        return new(operation, RmsDatabaseSecurityFailure.None, StatusCodes.Status200OK);
    }

    private static string OperationName(RmsDatabaseOperationKind operation) =>
        operation == RmsDatabaseOperationKind.Backup ? RmsDatabaseOperation.BackupOperationId : RmsDatabaseOperation.RestoreOperationId;

    private static RmsPrivilegedAuditEventKind ToAuditKind(RmsDatabaseWorkflowOutcome outcome) => outcome switch
    {
        RmsDatabaseWorkflowOutcome.Completed => RmsPrivilegedAuditEventKind.Completed,
        RmsDatabaseWorkflowOutcome.Failed => RmsPrivilegedAuditEventKind.Failed,
        RmsDatabaseWorkflowOutcome.OutcomeUnknown => RmsPrivilegedAuditEventKind.OutcomeUnknown,
        _ => RmsPrivilegedAuditEventKind.Failed
    };

    private static RmsDatabaseTarget ToContractTarget(RmsDatabaseKind database) => database switch
    {
        RmsDatabaseKind.Branch => RmsDatabaseTarget.Branch,
        RmsDatabaseKind.Cashier => RmsDatabaseTarget.Cashier,
        _ => throw new ArgumentOutOfRangeException(nameof(database))
    };

    private static RmsDatabaseArtifactDto ToArtifact(RmsApprovedDatabaseBackup artifact) => new(
        artifact.ArtifactId,
        artifact.DisplayName,
        artifact.SizeBytes,
        artifact.Sha256Checksum,
        artifact.CreatedAtUtc,
        artifact.ExpiresAtUtc);

    private static async IAsyncEnumerable<RmsDatabaseOperationDto> AsyncEnumerableEmpty()
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }
}
