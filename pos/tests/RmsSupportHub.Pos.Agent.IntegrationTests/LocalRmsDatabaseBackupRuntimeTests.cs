using RmsSupportHub.Pos.Agent;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Agent.RmsDatabase;
using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class LocalRmsDatabaseBackupRuntimeTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
    private const string Principal = "S-1-5-21-1111111111-2222222222-3333333333-1001";

    [Fact]
    public async Task CompletedBackupIsAuditedExactlyOnce()
    {
        var audit = new RecordingAuditSink();
        var storage = new RecordingStorage();
        var runtime = CreateRuntime(
            new ControlledWorkflow(_ => Task.FromResult(CompletedResult())),
            storage,
            audit,
            out var context);

        var started = await runtime.StartAsync(context, RmsDatabaseTarget.Branch, context.CorrelationId);
        var operation = await WaitForFinalAsync(runtime, context, started.Operation!, RmsDatabaseTarget.Branch);

        Assert.Equal(RmsDatabaseOperationOutcome.Completed, operation.Outcome);
        Assert.Single(audit.Events);
        Assert.Equal("completed", audit.Events[0].Outcome);
        Assert.Empty(storage.RevokedArtifactIds);
    }

    [Fact]
    public async Task AuditFailureRevokesNewArtifactAndNeverReportsSuccess()
    {
        var audit = new RecordingAuditSink { Result = false };
        var storage = new RecordingStorage();
        var runtime = CreateRuntime(
            new ControlledWorkflow(_ => Task.FromResult(CompletedResult())),
            storage,
            audit,
            out var context);

        var started = await runtime.StartAsync(context, RmsDatabaseTarget.Branch, context.CorrelationId);
        var operation = await WaitForFinalAsync(runtime, context, started.Operation!, RmsDatabaseTarget.Branch);

        Assert.Equal(RmsDatabaseOperationOutcome.Failed, operation.Outcome);
        Assert.Equal("audit_unavailable", operation.ErrorCode);
        Assert.Single(storage.RevokedArtifactIds);
        Assert.Single(audit.Events);
    }

    [Fact]
    public async Task SameTargetConcurrentBackupIsRejectedAndCancellationCleansUp()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var audit = new RecordingAuditSink();
        var storage = new RecordingStorage();
        var workflow = new ControlledWorkflow(async cancellationToken =>
        {
            entered.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CompletedResult();
        });
        var runtime = CreateRuntime(workflow, storage, audit, out var context);

        var first = await runtime.StartAsync(context, RmsDatabaseTarget.Cashier, context.CorrelationId);
        await entered.Task;
        var second = await runtime.StartAsync(
            context with { CorrelationId = "correlation-second" },
            RmsDatabaseTarget.Cashier,
            "correlation-second");

        Assert.False(second.Succeeded);
        Assert.Equal("operation_in_progress", second.ErrorCode);

        Assert.True(runtime.Cancel(context, RmsDatabaseTarget.Cashier, first.Operation!.OperationId));
        var operation = await WaitForFinalAsync(runtime, context, first.Operation, RmsDatabaseTarget.Cashier);
        Assert.Equal(RmsDatabaseOperationOutcome.Failed, operation.Outcome);
        Assert.Equal("cancelled", operation.ErrorCode);
        Assert.Empty(storage.RevokedArtifactIds);
    }

    [Theory]
    [InlineData(InvocationSource.LocalWpf, InvocationAuthorizationLevel.LocalOperator)]
    [InlineData(InvocationSource.RemoteHub, InvocationAuthorizationLevel.RemoteAdministrator)]
    [InlineData(InvocationSource.LocalWpf, InvocationAuthorizationLevel.Unauthenticated)]
    public async Task NonAdministratorOrRemoteCallersCannotStartBackup(
        InvocationSource source,
        InvocationAuthorizationLevel authority)
    {
        var runtime = CreateRuntime(
            new ControlledWorkflow(_ => Task.FromResult(CompletedResult())),
            new RecordingStorage(),
            new RecordingAuditSink(),
            out _,
            source,
            authority);
        var context = new InvocationContext(source, Principal, authority, "correlation-denied");

        var result = await runtime.StartAsync(context, RmsDatabaseTarget.Branch, context.CorrelationId);

        Assert.False(result.Succeeded);
        Assert.Equal("administrator_authorization_required", result.ErrorCode);
    }

    private static LocalRmsDatabaseBackupRuntime CreateRuntime(
        ControlledWorkflow workflow,
        RecordingStorage storage,
        RecordingAuditSink audit,
        out InvocationContext context,
        InvocationSource source = InvocationSource.LocalWpf,
        InvocationAuthorizationLevel authority = InvocationAuthorizationLevel.LocalAdministrator)
    {
        var clock = new ManualTimeProvider(Start);
        var handler = new RmsDatabaseBackupQueryHandler(workflow, storage, clock);
        var runtime = new LocalRmsDatabaseBackupRuntime(
            handler,
            new RmsDatabaseOperationStore(clock, RuntimeRetentionPolicy.Default),
            new RmsDatabaseConcurrencyGate(),
            storage,
            audit,
            clock);
        context = new InvocationContext(source, Principal, authority, "correlation-runtime");
        return runtime;
    }

    private static async Task<RmsDatabaseOperationDto> WaitForFinalAsync(
        LocalRmsDatabaseBackupRuntime runtime,
        InvocationContext context,
        RmsDatabaseOperationDto initial,
        RmsDatabaseTarget target)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (runtime.TryGet(context, target, initial.OperationId, out var operation)
                && operation is not null
                && operation.State is (RmsDatabaseOperationState.Completed
                    or RmsDatabaseOperationState.Failed
                    or RmsDatabaseOperationState.OutcomeUnknown))
            {
                return operation;
            }

            await Task.Delay(10);
        }

        Assert.Fail("The local backup operation did not reach a final state.");
        return initial;
    }

    private static RmsDatabaseWorkflowResult CompletedResult() =>
        new(
            RmsDatabaseWorkflowOutcome.Completed,
            "backup_completed",
            "completed",
            new(
                RmsDatabaseKind.Branch,
                "0123456789abcdef0123456789abcdef",
                "branch.bak",
                10,
                new string('a', 64),
                Start,
                Start.AddDays(1),
                "internal-only.bak",
                PrincipalSid: Principal),
            false,
            false,
            []);

    private sealed class ControlledWorkflow(Func<CancellationToken, Task<RmsDatabaseWorkflowResult>> backup) : IRmsDatabaseWorkflow
    {
        public Task<RmsDatabaseWorkflowResult> BackupAsync(
            RmsDatabaseKind database,
            IProgress<RmsDatabaseProgress>? progress = null,
            CancellationToken cancellationToken = default,
            string? principalSid = null) => backup(cancellationToken);

        public Task<RmsDatabaseWorkflowResult> RestoreAsync(
            RmsDatabaseKind database,
            string artifactId,
            IProgress<RmsDatabaseProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingStorage : IRmsDatabaseBackupStorage
    {
        public List<string> RevokedArtifactIds { get; } = [];

        public Task<RmsDatabaseBackupAllocation> AllocateAsync(RmsDatabaseKind database, DateTimeOffset createdAtUtc, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RmsApprovedDatabaseBackup?> RegisterAsync(RmsDatabaseKind database, RmsDatabaseBackupAllocation allocation, CancellationToken cancellationToken = default, string? principalSid = null) =>
            throw new NotSupportedException();

        public Task<RmsApprovedDatabaseBackup?> ResolveAsync(RmsDatabaseKind database, string artifactId, CancellationToken cancellationToken = default, string? principalSid = null) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RmsApprovedDatabaseBackup>> ListAsync(RmsDatabaseKind database, CancellationToken cancellationToken = default, string? principalSid = null) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RmsApprovedDatabaseBackup>> ListInventoryAsync(RmsDatabaseKind database, string principalSid, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RevokeAsync(RmsDatabaseKind database, string artifactId, string principalSid, CancellationToken cancellationToken = default)
        {
            RevokedArtifactIds.Add(artifactId);
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingAuditSink : IAgentAuditSink
    {
        public bool Result { get; set; } = true;
        public List<AgentAuditEvent> Events { get; } = [];

        public bool Record(AgentAuditEvent auditEvent)
        {
            Events.Add(auditEvent);
            return Result;
        }
    }
}
