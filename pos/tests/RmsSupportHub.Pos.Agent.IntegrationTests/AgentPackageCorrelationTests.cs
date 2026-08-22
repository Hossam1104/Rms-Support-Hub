using RmsSupportHub.Pos.Agent;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Packages;
using RmsSupportHub.Pos.Agent.Runtime;
using RmsSupportHub.Pos.Contracts.V1.Packages;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class AgentPackageCorrelationTests
{
    [Fact]
    public async Task CompletedAuditAndTimelineUseTheOpaqueOperationInstanceId()
    {
        var clock = TimeProvider.System;
        var retention = RuntimeRetentionPolicy.Default;
        var manifest = CreateManifest();
        var audit = new RecordingAuditSink();
        var timeline = new IncidentTimelineService(new IncidentTimelineStore(clock));
        var service = new AgentPackageService(
            new FixedCatalog(manifest),
            new AcceptingVerifier(),
            new CompletingLifecycle(),
            new UnusedSnapshotStore(),
            new AgentPackagePreviewStore(retention, clock),
            new AgentPackageOperationStore(retention, clock),
            new AgentScopedIdempotencyStore(retention, clock),
            new AcquiringLease(),
            timeline,
            audit,
            clock);

        const string principalSid = "S-1-5-21-1000";
        const string correlationId = "corr-agent-package-test";
        var preview = await service.PreviewAsync(principalSid, AgentPackageOperationKind.Health, "preview-key");
        var accepted = await service.StartAsync(
            principalSid,
            new AgentPackageOperationRequestDto(
                preview.PreviewId,
                preview.ConfirmationPhrase,
                "operation-key"),
            correlationId);

        AgentPackageOperationDto? completed = null;
        for (var attempt = 0; attempt < 100 && completed?.State is not AgentPackageOperationStateDto.Completed; attempt++)
        {
            service.TryGetOperation(principalSid, accepted.OperationId, out completed);
            if (completed?.State is not AgentPackageOperationStateDto.Completed) await Task.Delay(10);
        }

        Assert.NotNull(completed);
        Assert.Equal(AgentPackageOperationStateDto.Completed, completed!.State);

        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (audit.Events.Any(item => item.Outcome == nameof(AgentPackageOperationState.Completed))
                && timeline.Get(principalSid).Events.Any(item => item.Kind == "AgentPackage")) break;
            await Task.Delay(10);
        }

        var completionAudit = Assert.Single(audit.Events, item => item.Outcome == nameof(AgentPackageOperationState.Completed));
        Assert.Equal(accepted.OperationId, completionAudit.Target);
        Assert.Equal(correlationId, completionAudit.CorrelationId);
        Assert.NotEqual(AgentPackageOperation.OperationId, completionAudit.Target);

        var timelineEvent = Assert.Single(timeline.Get(principalSid).Events, item => item.Kind == "AgentPackage");
        Assert.Equal(accepted.OperationId, timelineEvent.OperationId);
        Assert.Equal(correlationId, timelineEvent.CorrelationId);
    }

    private static AgentPackageManifest CreateManifest() =>
        new(
            "rms-support-agent",
            "1.0.0",
            "Windows",
            "net10.0-windows",
            AgentProductIdentity.ServiceDisplayName,
            "LocalSystem",
            AgentProductIdentity.PermanentServiceName,
            "SHA256withRSA",
            "DBS test signer",
            new string('a', 64),
            "test-signature",
            [new AgentPackageFileManifest("agent", "RmsSupportAgent.exe", 1, new string('b', 64), true)],
            ["AgentServiceReadWrite"],
            ["AgentHttpsCertificate"],
            null,
            false,
            1,
            1,
            AgentProductIdentity.ProductId,
            "x64",
            AgentProductIdentity.ReleaseChannelTesting,
            AgentProductIdentity.EnvironmentTesting,
            AgentProductIdentity.ServiceDescription);

    private sealed class FixedCatalog(AgentPackageManifest manifest) : IAgentPackageCatalog
    {
        public Task<AgentPackageManifest?> GetAvailableAsync(AgentPackageOperationKind operation, CancellationToken cancellationToken = default) => Task.FromResult<AgentPackageManifest?>(manifest);

        public Task<AgentPackageManifest?> GetInstalledAsync(CancellationToken cancellationToken = default) => Task.FromResult<AgentPackageManifest?>(manifest);
    }

    private sealed class AcceptingVerifier : IAgentPackageVerifier
    {
        public Task<AgentPackageValidationResult> VerifyAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default) => Task.FromResult(Verified());

        public Task<AgentPackageValidationResult> VerifyInstalledAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default) => Task.FromResult(Verified());

        private static AgentPackageValidationResult Verified() => new(AgentPackageVerificationState.Verified, [], "verified");
    }

    private sealed class CompletingLifecycle : IAgentPackageLifecycle
    {
        public Task<AgentPackageExecutionResult> ExecuteAsync(AgentPackageExecutionRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentPackageExecutionResult(AgentPackageOperationState.Completed, false, false, false, "completed"));
    }

    private sealed class UnusedSnapshotStore : ISafetySnapshotStore
    {
        public Task SaveAsync(SafetySnapshotDocument snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<SafetySnapshotVerificationResult> ReadAndVerifyAsync(string principalSid, string snapshotId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SafetySnapshotVerificationResult(SafetySnapshotState.Unknown, false, null, "unused"));

        public Task<int> PruneAsync(DateTimeOffset now, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class AcquiringLease : IPrivilegedMutationLease
    {
        public PrivilegedMutationLeaseAttempt TryAcquire(string operationScope, string principalSid) =>
            new(PrivilegedMutationLeaseState.Acquired, new NoopDisposable(), "acquired");
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private sealed class RecordingAuditSink : IAgentAuditSink
    {
        private readonly object gate = new();
        private readonly List<AgentAuditEvent> events = [];

        public IReadOnlyList<AgentAuditEvent> Events
        {
            get
            {
                lock (gate) return events.ToArray();
            }
        }

        public bool Record(AgentAuditEvent auditEvent)
        {
            lock (gate) events.Add(auditEvent);
            return true;
        }
    }
}
