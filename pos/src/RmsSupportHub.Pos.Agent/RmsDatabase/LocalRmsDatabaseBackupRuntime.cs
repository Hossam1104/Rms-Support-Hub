using System.Collections.Concurrent;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Contracts.V1.Rms;
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
    IAgentAuditSink audit,
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
            var progress = new Progress<RmsDatabaseProgress>(update => operations.Progress(handle.OperationId, update));
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
                    "cancelled",
                    "The database backup was cancelled.",
                    null,
                    false,
                    false,
                    []);
                return;
            }

            var outcome = workflowResult.Outcome;
            var code = workflowResult.Code;
            var detail = workflowResult.Detail;
            var artifact = workflowResult.Backup;
            if (outcome == RmsDatabaseWorkflowOutcome.Completed
                && !TryRecordAudit(
                    context,
                    database,
                    correlationId,
                    "completed",
                    null))
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
                code = "audit_unavailable";
                detail = "The backup completed but its required audit record could not be persisted.";
                artifact = null;
            }
            else if (outcome != RmsDatabaseWorkflowOutcome.Completed)
            {
                TryRecordAudit(context, database, correlationId, outcome.ToString().ToLowerInvariant(), code);
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
        catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
        {
            TryRecordAudit(context, database, correlationId, "cancelled", "cancelled");
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
            TryRecordAudit(context, database, correlationId, "failed", "backup_failed");
            operations.Complete(
                handle.OperationId,
                RmsDatabaseWorkflowOutcome.Failed,
                "backup_failed",
                "The RMS database backup could not be completed.",
                null,
                false,
                false,
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
        string code,
        string detail,
        RmsApprovedDatabaseBackup? artifact,
        bool destructiveAttempted,
        bool recoveryRequired,
        IReadOnlyList<string> warnings)
    {
        TryRecordAudit(context, database, correlationId, outcome.ToString().ToLowerInvariant(), code);
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
        string outcome,
        string? failureCode)
    {
        try
        {
            return audit.Record(new AgentAuditEvent(
                timeProvider.GetUtcNow(),
                context.AuthenticatedCaller,
                "rms.database.backup.create",
                database == RmsDatabaseKind.Branch ? "branch" : "cashier",
                correlationId,
                outcome,
                failureCode,
                typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "unavailable",
                null)
            {
                Source = InvocationSource.LocalWpf.ToString()
            });
        }
        catch
        {
            return false;
        }
    }

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
