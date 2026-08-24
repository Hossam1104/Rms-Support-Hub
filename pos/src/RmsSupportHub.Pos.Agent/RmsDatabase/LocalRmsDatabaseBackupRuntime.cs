using System.Collections.Concurrent;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Exceptions;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.RmsDatabase;

/// <summary>
/// Local IPC adapter for the existing typed database backup workflow. It shares the database
/// operation store and per-target lease with the legacy Agent boundary, while deriving authority
/// and principal scope from the trusted Local IPC invocation context.
/// </summary>
public sealed class LocalRmsDatabaseBackupRuntime(
    RmsDatabaseBackupQueryHandler handler,
    RmsDatabaseOperationStore operations,
    RmsDatabaseConcurrencyGate concurrency,
    IRmsDatabaseBackupStorage storage,
    IRmsPrivilegedAuditSink audit,
    TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> cancellations = new(StringComparer.Ordinal);

    public async Task<LocalRmsDatabaseOperationResult> StartAsync(
        InvocationContext context,
        RmsDatabaseTarget target,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var decision = AgentOperationAuthorization.Authorize(
            context,
            AgentOperationRisk.AdministratorOnlyMutation);
        if (!decision.Allowed)
        {
            return LocalRmsDatabaseOperationResult.Failure(decision.Code, decision.Message);
        }

        if (!TryResolve(target, out var database)
            || !IsSafeCorrelation(correlationId)
            || !string.Equals(context.CorrelationId, correlationId, StringComparison.Ordinal)
            || !IsSafeSid(context.AuthenticatedCaller))
        {
            return LocalRmsDatabaseOperationResult.Failure(
                "invalid_request",
                "The backup request was invalid.");
        }

        if (!TryRecordAudit(
                context,
                database,
                correlationId,
                RmsPrivilegedAuditEventKind.Requested,
                "The local typed RMS database backup was requested."))
        {
            return LocalRmsDatabaseOperationResult.Failure(
                "audit_unavailable",
                "The backup is temporarily unavailable because required audit recording is unavailable.");
        }

        var lease = concurrency.TryEnter(database);
        if (lease is null)
        {
            return LocalRmsDatabaseOperationResult.Failure(
                "operation_in_progress",
                "Another backup operation for this database is already in progress.");
        }

        RmsDatabaseOperationHandle handle;
        try
        {
            handle = operations.Create(
                context.AuthenticatedCaller,
                database,
                RmsDatabaseOperationKind.Backup,
                correlationId);
        }
        catch (RmsDatabaseOperationCapacityException)
        {
            lease.Dispose();
            return LocalRmsDatabaseOperationResult.Failure(
                "operation_capacity",
                "The Agent cannot retain another backup operation at this time.");
        }

        var operationCancellation = new CancellationTokenSource();
        if (!cancellations.TryAdd(handle.OperationId, operationCancellation))
        {
            operationCancellation.Dispose();
            lease.Dispose();
            return LocalRmsDatabaseOperationResult.Failure(
                "operation_in_progress",
                "The backup operation could not be registered safely.");
        }

        if (!TryRecordAudit(
                context,
                database,
                correlationId,
                RmsPrivilegedAuditEventKind.Accepted,
                "The local typed RMS database backup passed preflight and was accepted."))
        {
            cancellations.TryRemove(handle.OperationId, out var failedCancellation);
            failedCancellation?.Dispose();
            operations.Remove(handle.OperationId);
            lease.Dispose();
            return LocalRmsDatabaseOperationResult.Failure(
                "audit_unavailable",
                "The backup is temporarily unavailable because required audit recording is unavailable.");
        }

        operations.Start(handle.OperationId);
        _ = Task.Run(
            () => RunAsync(handle, context, database, correlationId, lease, operationCancellation),
            CancellationToken.None);
        await Task.CompletedTask.ConfigureAwait(false);
        return LocalRmsDatabaseOperationResult.Success(handle.InitialState with
        {
            State = RmsDatabaseOperationState.Running,
            Outcome = RmsDatabaseOperationOutcome.Accepted,
            ProgressPercent = 1,
            Stage = "running",
            Detail = "The Agent is running the server-owned database backup."
        });
    }

    public bool TryGet(
        InvocationContext context,
        RmsDatabaseTarget target,
        string operationId,
        out RmsDatabaseOperationDto? operation)
    {
        operation = null;
        if (!IsAdministrator(context)
            || !TryResolve(target, out var database)
            || !IsSafeToken(operationId))
        {
            return false;
        }

        return operations.TryGet(context.AuthenticatedCaller, database, operationId, out operation);
    }

    public bool Cancel(
        InvocationContext context,
        RmsDatabaseTarget target,
        string operationId)
    {
        return TryGet(context, target, operationId, out _)
            && cancellations.TryGetValue(operationId, out var cancellation)
            && !cancellation.IsCancellationRequested
            && TryCancel(cancellation);
    }

    private async Task RunAsync(
        RmsDatabaseOperationHandle handle,
        InvocationContext context,
        RmsDatabaseKind database,
        string correlationId,
        RmsDatabaseLease lease,
        CancellationTokenSource operationCancellation)
    {
        try
        {
            if (!TryRecordAudit(
                    context,
                    database,
                    correlationId,
                    RmsPrivilegedAuditEventKind.Started,
                    "The local typed RMS database backup started."))
            {
                CompleteAndAudit(
                    handle,
                    context,
                    database,
                    correlationId,
                    RmsDatabaseWorkflowOutcome.Failed,
                    RmsPrivilegedAuditEventKind.Failed,
                    "audit_unavailable",
                    "The backup was not started because required audit recording is unavailable.",
                    null,
                    false,
                    false,
                    []);
                return;
            }

            var progress = new InlineProgress(update =>
            {
                operations.Progress(handle.OperationId, update);
                if (update.Stage == "dispatch")
                {
                    if (!TryRecordAudit(
                            context,
                            database,
                            correlationId,
                            RmsPrivilegedAuditEventKind.Dispatch,
                            update.Detail))
                    {
                        throw new RmsDatabaseAuditUnavailableException();
                    }
                }
            });
            var result = await handler
                .CreateAsync(context, database, progress, operationCancellation.Token)
                .ConfigureAwait(false);

            if (!result.Succeeded || result.Value is null)
            {
                CompleteAndAudit(
                    handle,
                    context,
                    database,
                    correlationId,
                    RmsDatabaseWorkflowOutcome.Failed,
                    RmsPrivilegedAuditEventKind.Failed,
                    result.Code,
                    result.Detail,
                    null,
                    false,
                    false,
                    []);
                return;
            }

            var workflowResult = result.Value;
            if (operationCancellation.IsCancellationRequested)
            {
                if (workflowResult.Backup is not null)
                {
                    await storage.RevokeAsync(
                        database,
                        workflowResult.Backup.ArtifactId,
                        context.AuthenticatedCaller,
                        CancellationToken.None).ConfigureAwait(false);
                }

                CompleteAndAudit(
                    handle,
                    context,
                    database,
                    correlationId,
                    RmsDatabaseWorkflowOutcome.Failed,
                    RmsPrivilegedAuditEventKind.Cancelled,
                    "cancelled",
                    "The database backup was cancelled.",
                    null,
                    false,
                    false,
                    []);
                return;
            }

            var outcome = workflowResult.Outcome == RmsDatabaseWorkflowOutcome.NotAttempted
                ? RmsDatabaseWorkflowOutcome.Failed
                : workflowResult.Outcome;
            var code = workflowResult.Code;
            var detail = workflowResult.Detail;
            var artifact = workflowResult.Backup;
            var terminalKind = ToAuditKind(outcome);
            if (!TryRecordAudit(context, database, correlationId, terminalKind, detail))
            {
                if (outcome == RmsDatabaseWorkflowOutcome.Completed)
                {
                    if (artifact is not null)
                    {
                        await storage.RevokeAsync(
                            database,
                            artifact.ArtifactId,
                            context.AuthenticatedCaller,
                            CancellationToken.None).ConfigureAwait(false);
                    }

                    outcome = RmsDatabaseWorkflowOutcome.Failed;
                    artifact = null;
                }

                code = "audit_unavailable";
                detail = outcome == RmsDatabaseWorkflowOutcome.Failed
                    ? "The backup outcome was not durably audited."
                    : "The backup outcome is unknown because its required audit record could not be persisted.";
            }

            operations.Complete(
                handle.OperationId,
                outcome,
                code,
                detail,
                artifact,
                workflowResult.DestructiveAttempted,
                workflowResult.RecoveryRequired,
                workflowResult.Warnings);
        }
        catch (RmsDatabaseAuditUnavailableException)
        {
            CompleteAndAudit(
                handle,
                context,
                database,
                correlationId,
                RmsDatabaseWorkflowOutcome.Failed,
                RmsPrivilegedAuditEventKind.Failed,
                "audit_unavailable",
                "The backup was not dispatched because required audit recording is unavailable.",
                null,
                false,
                false,
                []);
        }
        catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
        {
            TryRecordAudit(
                context,
                database,
                correlationId,
                RmsPrivilegedAuditEventKind.Cancelled,
                "The database backup was cancelled.");
            operations.Complete(
                handle.OperationId,
                RmsDatabaseWorkflowOutcome.Failed,
                "cancelled",
                "The database backup was cancelled.",
                null,
                false,
                false,
                []);
        }
        catch
        {
            TryRecordAudit(
                context,
                database,
                correlationId,
                RmsPrivilegedAuditEventKind.OutcomeUnknown,
                "The database backup outcome is unknown.");
            operations.Complete(
                handle.OperationId,
                RmsDatabaseWorkflowOutcome.OutcomeUnknown,
                "database_operation_outcome_unknown",
                "The database backup failed ambiguously. Check database and service state before any retry.",
                null,
                true,
                true,
                []);
        }
        finally
        {
            cancellations.TryRemove(handle.OperationId, out var removed);
            removed?.Dispose();
            lease.Dispose();
        }
    }

    private void CompleteAndAudit(
        RmsDatabaseOperationHandle handle,
        InvocationContext context,
        RmsDatabaseKind database,
        string correlationId,
        RmsDatabaseWorkflowOutcome outcome,
        RmsPrivilegedAuditEventKind auditKind,
        string code,
        string detail,
        RmsApprovedDatabaseBackup? artifact,
        bool destructiveAttempted,
        bool recoveryRequired,
        IReadOnlyList<string> warnings)
    {
        TryRecordAudit(context, database, correlationId, auditKind, detail);
        operations.Complete(
            handle.OperationId,
            outcome,
            code,
            detail,
            artifact,
            destructiveAttempted,
            recoveryRequired,
            warnings);
    }

    private bool TryRecordAudit(
        InvocationContext context,
        RmsDatabaseKind database,
        string correlationId,
        RmsPrivilegedAuditEventKind kind,
        string detail)
    {
        try
        {
            return audit.Record(new RmsPrivilegedAuditEvent(
                timeProvider.GetUtcNow(),
                kind,
                database,
                "rms.database.backup",
                correlationId,
                context.AuthenticatedCaller,
                detail));
        }
        catch
        {
            return false;
        }
    }

    private static RmsPrivilegedAuditEventKind ToAuditKind(RmsDatabaseWorkflowOutcome outcome) => outcome switch
    {
        RmsDatabaseWorkflowOutcome.Completed => RmsPrivilegedAuditEventKind.Completed,
        RmsDatabaseWorkflowOutcome.OutcomeUnknown => RmsPrivilegedAuditEventKind.OutcomeUnknown,
        _ => RmsPrivilegedAuditEventKind.Failed
    };

    private static bool IsAdministrator(InvocationContext context) =>
        AgentOperationAuthorization.Authorize(context, AgentOperationRisk.AdministratorOnlyMutation).Allowed;

    private static bool TryResolve(RmsDatabaseTarget target, out RmsDatabaseKind database)
    {
        database = target switch
        {
            RmsDatabaseTarget.Branch => RmsDatabaseKind.Branch,
            RmsDatabaseTarget.Cashier => RmsDatabaseKind.Cashier,
            _ => default
        };
        return target is RmsDatabaseTarget.Branch or RmsDatabaseTarget.Cashier;
    }

    private static bool IsSafeSid(string? value) =>
        value is { Length: > 0 and <= 184 }
        && value.StartsWith("S-", StringComparison.OrdinalIgnoreCase)
        && value.All(character => char.IsLetterOrDigit(character) || character == '-');

    private static bool IsSafeCorrelation(string? value) =>
        value is { Length: > 0 and <= 128 }
        && value.All(character => character is >= '!' and <= '~');

    private static bool IsSafeToken(string? value) => IsSafeCorrelation(value);

    private static bool TryCancel(CancellationTokenSource cancellation)
    {
        try
        {
            cancellation.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private sealed class InlineProgress(Action<RmsDatabaseProgress> callback) : IProgress<RmsDatabaseProgress>
    {
        public void Report(RmsDatabaseProgress value) => callback(value);
    }

}

public sealed record LocalRmsDatabaseOperationResult(
    bool Succeeded,
    RmsDatabaseOperationDto? Operation,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static LocalRmsDatabaseOperationResult Success(RmsDatabaseOperationDto operation) =>
        new(true, operation, null, null);

    public static LocalRmsDatabaseOperationResult Failure(string code, string message) =>
        new(false, null, code, message);
}
