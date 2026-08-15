using System.Security.Cryptography;
using System.Text;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.Runtime;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Snapshots;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Snapshots;

public static class SafetySnapshotOperation
{
    public const string CaptureOperationId = "safety-snapshot.capture";
    public const string CaptureHttpPath = "/api/v1/safety-snapshots/capture";

    public static MutationTokens.MutationOperationDescriptor Descriptor { get; } =
        new(CaptureOperationId, "POST", CaptureHttpPath, MutationTokens.MutationTargetKind.None);
}

public sealed class SafetySnapshotService(
    ISafetySnapshotEvidenceSource evidenceSource,
    ISafetySnapshotStore store,
    AgentScopedIdempotencyStore idempotency,
    TimeProvider clock,
    RmsSupportHub.Pos.Infrastructure.Snapshots.SafetySnapshotOptions options)
{
    public const string ConfirmationPhrase = "CAPTURE SAFETY SNAPSHOT";
    private const string IdempotencyScope = "safety-snapshot.capture";
    private readonly object gate = new();
    private readonly Dictionary<string, SafetySnapshotDto> completedCaptures = new(StringComparer.Ordinal);

    public async Task<SafetySnapshotPreviewDto> PreviewAsync(CancellationToken cancellationToken = default)
    {
        var evidence = await evidenceSource.CaptureAsync(cancellationToken).ConfigureAwait(false);
        var blockers = evidence.State == SafetySnapshotEvidenceState.ActionRequired
            ? ["health_evidence_requires_attention"]
            : evidence.State == SafetySnapshotEvidenceState.Unknown
                ? ["safe_evidence_unavailable"]
                : Array.Empty<string>();
        return new(
            "pre-maintenance",
            blockers.Length == 0,
            Map(evidence.State),
            ["safe identity", "Product Release and component drift", "canonical service state", "database identity and reachability", "fixed-root capacity", "approved-backup metadata", "configuration fingerprint", "correlation ID"],
            ["credentials", "raw configuration", "SQL", "raw logs", "private keys", "arbitrary paths", "full events"],
            blockers,
            checked((int)Math.Ceiling(options.Lifetime.TotalMinutes)),
            clock.GetUtcNow().Add(options.Lifetime));
    }

    public async Task<SafetySnapshotDto> CaptureAsync(
        string principalSid,
        string typedConfirmation,
        string correlationId,
        string idempotencyKey,
        string? profileId = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(typedConfirmation, ConfirmationPhrase, StringComparison.Ordinal))
        {
            throw new SafetySnapshotRejectedException("snapshot_confirmation_invalid");
        }

        if (!AgentScopedIdempotencyStore.IsValidKey(idempotencyKey))
        {
            throw new SafetySnapshotRejectedException("snapshot_idempotency_invalid");
        }

        var reservation = idempotency.TryReserve(principalSid, IdempotencyScope, idempotencyKey, typedConfirmation);
        if (reservation.State == AgentIdempotencyReservationState.Completed
            && reservation.OperationId is not null
            && completedCaptures.TryGetValue(principalSid + "|" + reservation.OperationId, out var existing))
        {
            return existing;
        }
        if (reservation.State != AgentIdempotencyReservationState.Reserved)
        {
            throw new SafetySnapshotRejectedException("snapshot_idempotency_conflict");
        }

        try
        {
            var evidence = await evidenceSource.CaptureAsync(cancellationToken).ConfigureAwait(false);
            var now = clock.GetUtcNow();
            var snapshot = new SafetySnapshotDocument(
                Guid.NewGuid().ToString("N"),
                principalSid,
                "Testing",
                profileId,
                evidence.BranchCode,
                evidence.PosNumber,
                evidence.InstallationGuid,
                evidence.ClientName,
                evidence.ProductRelease,
                evidence.PackageVersion,
                evidence.PackageHash,
                evidence.ConfigurationFingerprint ?? "unavailable",
                evidence.Services,
                evidence.Databases,
                evidence.Capacity,
                evidence.Backups,
                evidence.State,
                correlationId,
                now,
                now.Add(options.Lifetime),
                string.Empty);
            await store.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
            var response = ToDto(snapshot, SafetySnapshotState.Captured, "The pre-maintenance safety snapshot was captured atomically.");
            lock (gate) completedCaptures[principalSid + "|" + snapshot.SnapshotId] = response;
            idempotency.Bind(principalSid, IdempotencyScope, idempotencyKey, snapshot.SnapshotId);
            return response;
        }
        catch
        {
            idempotency.Release(principalSid, IdempotencyScope, idempotencyKey);
            throw;
        }
    }

    public async Task<SafetySnapshotVerificationDto> VerifyAsync(string principalSid, string snapshotId, CancellationToken cancellationToken = default)
    {
        var result = await store.ReadAndVerifyAsync(principalSid, snapshotId, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return new(snapshotId, Map(result.State), result.Verified, result.Detail, clock.GetUtcNow(), result.Snapshot?.CorrelationId);
    }

    public async Task<SafetySnapshotDto?> GetAsync(string principalSid, string snapshotId, CancellationToken cancellationToken = default)
    {
        var result = await store.ReadAndVerifyAsync(principalSid, snapshotId, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return result.Snapshot is null ? null : ToDto(result.Snapshot, result.State, result.Detail);
    }

    private static SafetySnapshotDto ToDto(SafetySnapshotDocument snapshot, SafetySnapshotState state, string detail) =>
        new(
            snapshot.SnapshotId,
            Map(state),
            Map(snapshot.EvidenceState),
            snapshot.BranchCode,
            snapshot.PosNumber,
            snapshot.InstallationGuid,
            snapshot.ClientName,
            snapshot.ProductRelease,
            snapshot.PackageVersion,
            snapshot.PackageHash,
            snapshot.ConfigurationFingerprint,
            snapshot.Services.Select(service => new SafetySnapshotServiceEvidenceDto(service.ServiceId, service.State, Map(service.EvidenceState))).ToArray(),
            snapshot.Databases.Select(database => new SafetySnapshotDatabaseEvidenceDto(database.DatabaseId, database.State, Map(database.EvidenceState), database.Identity)).ToArray(),
            new(Map(snapshot.Capacity.State), snapshot.Capacity.AvailableBytes, snapshot.Capacity.Detail),
            new(Map(snapshot.Backups.State), snapshot.Backups.ApprovedCount, snapshot.Backups.LatestCreatedAtUtc, snapshot.Backups.Detail),
            snapshot.CorrelationId,
            snapshot.CapturedAtUtc,
            snapshot.ExpiresAtUtc,
            detail);

    private static SafetySnapshotEvidenceStateDto Map(SafetySnapshotEvidenceState state) => state switch
    {
        SafetySnapshotEvidenceState.Healthy => SafetySnapshotEvidenceStateDto.Healthy,
        SafetySnapshotEvidenceState.Warning => SafetySnapshotEvidenceStateDto.Warning,
        SafetySnapshotEvidenceState.ActionRequired => SafetySnapshotEvidenceStateDto.ActionRequired,
        _ => SafetySnapshotEvidenceStateDto.Unknown
    };

    private static SafetySnapshotStateDto Map(SafetySnapshotState state) => Enum.TryParse<SafetySnapshotStateDto>(state.ToString(), out var mapped) ? mapped : SafetySnapshotStateDto.Unknown;

    private static string Map(MainServerEnvironment environment) => environment.ToString();
}

public sealed class SafetySnapshotRejectedException(string code) : InvalidOperationException(code);

/// <summary>Maps the existing Slice A typed diagnostics into the stricter snapshot evidence model.</summary>
public sealed class RmsSafetySnapshotEvidenceSource(Rms.RmsDiagnosticsService diagnostics) : ISafetySnapshotEvidenceSource
{
    public async Task<SafetySnapshotEvidence> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var rms = await diagnostics.GetAsync(cancellationToken).ConfigureAwait(false);
        var identity = rms.Installation;
        var services = rms.Services.Select(service => new SafetySnapshotServiceEvidence(
            service.ServiceId,
            service.State.ToString(),
            service.State == ServiceRuntimeState.Running ? SafetySnapshotEvidenceState.Healthy :
                service.State == ServiceRuntimeState.Unknown ? SafetySnapshotEvidenceState.Unknown : SafetySnapshotEvidenceState.Warning)).ToArray();
        var databases = new[]
        {
            new SafetySnapshotDatabaseEvidence("branch", rms.BranchDatabase.ConnectivityStatus.ToString(), ToState(rms.BranchDatabase.Health.Backups.State), rms.BranchDatabase.ConfiguredDatabase),
            new SafetySnapshotDatabaseEvidence("cashier", rms.CashierDatabase.ConnectivityStatus.ToString(), ToState(rms.CashierDatabase.Health.Backups.State), rms.CashierDatabase.ConfiguredDatabase)
        };
        var available = new[] { rms.BranchDatabase.Health.Storage.AvailableFreeSpaceBytes, rms.CashierDatabase.Health.Storage.AvailableFreeSpaceBytes }.Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty().Min();
        var capacityState = rms.BranchDatabase.Health.Storage.State == HealthState.Healthy && rms.CashierDatabase.Health.Storage.State == HealthState.Healthy
            ? SafetySnapshotEvidenceState.Healthy : SafetySnapshotEvidenceState.Warning;
        var backupCount = rms.BranchDatabase.Health.Backups.Count + rms.CashierDatabase.Health.Backups.Count;
        var latest = new[] { rms.BranchDatabase.Health.Backups.LatestCreatedAtUtc, rms.CashierDatabase.Health.Backups.LatestCreatedAtUtc }.Where(value => value.HasValue).Select(value => value!.Value).OrderByDescending(value => value).FirstOrDefault();
        var configurationFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", identity.BranchCode, identity.PosNumber, identity.InstallationGuid, identity.ClientName, identity.ProductRelease, identity.Versions.BranchServerBuildNumber, identity.Versions.CashierServerBuildNumber, identity.Versions.CashierUiBuildNumber)))).ToLowerInvariant();
        var overall = identity.Consistency.Warnings.Count > 0 || services.Any(service => service.EvidenceState == SafetySnapshotEvidenceState.ActionRequired)
            ? SafetySnapshotEvidenceState.ActionRequired
            : identity.Installed ? SafetySnapshotEvidenceState.Healthy : SafetySnapshotEvidenceState.Unknown;
        return new(identity.BranchCode, identity.PosNumber, identity.InstallationGuid, identity.ClientName, identity.ProductRelease, configurationFingerprint, null, null, services, databases,
            new(capacityState, available == 0 ? null : available, "Capacity is reported only for the fixed Agent storage roots."),
            new(backupCount > 0 ? SafetySnapshotEvidenceState.Healthy : SafetySnapshotEvidenceState.Warning, backupCount, latest == default ? null : latest, "Only approved backup metadata is retained."), overall);
    }

    private static SafetySnapshotEvidenceState ToState(HealthState state) => state switch
    {
        HealthState.Healthy => SafetySnapshotEvidenceState.Healthy,
        HealthState.ActionRequired => SafetySnapshotEvidenceState.ActionRequired,
        HealthState.Warning => SafetySnapshotEvidenceState.Warning,
        _ => SafetySnapshotEvidenceState.Unknown
    };
}
