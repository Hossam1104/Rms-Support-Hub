namespace RmsSupportHub.Pos.Domain.Models;

public enum SafetySnapshotState
{
    Preview,
    Captured,
    Verified,
    Stale,
    Expired,
    Mismatched,
    Corrupt,
    Unavailable,
    Unknown
}

public enum SafetySnapshotEvidenceState
{
    Healthy,
    Warning,
    ActionRequired,
    Unknown
}

public sealed record SafetySnapshotServiceEvidence(
    string ServiceId,
    string State,
    SafetySnapshotEvidenceState EvidenceState);

public sealed record SafetySnapshotDatabaseEvidence(
    string DatabaseId,
    string State,
    SafetySnapshotEvidenceState EvidenceState,
    string? Identity);

public sealed record SafetySnapshotCapacityEvidence(
    SafetySnapshotEvidenceState State,
    long? AvailableBytes,
    string Detail);

public sealed record SafetySnapshotBackupEvidence(
    SafetySnapshotEvidenceState State,
    int ApprovedCount,
    DateTimeOffset? LatestCreatedAtUtc,
    string Detail);

/// <summary>Persisted safe snapshot document. It deliberately contains no path, SQL, secret, or raw log fields.</summary>
public sealed record SafetySnapshotDocument(
    string SnapshotId,
    string PrincipalSid,
    string Environment,
    string? ProfileId,
    string? BranchCode,
    string? PosNumber,
    string? InstallationGuid,
    string? ClientName,
    string? ProductRelease,
    string? PackageVersion,
    string? PackageHash,
    string ConfigurationFingerprint,
    IReadOnlyList<SafetySnapshotServiceEvidence> Services,
    IReadOnlyList<SafetySnapshotDatabaseEvidence> Databases,
    SafetySnapshotCapacityEvidence Capacity,
    SafetySnapshotBackupEvidence Backups,
    SafetySnapshotEvidenceState EvidenceState,
    string CorrelationId,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string IntegrityHash);

public sealed record SafetySnapshotVerificationResult(
    SafetySnapshotState State,
    bool Verified,
    SafetySnapshotDocument? Snapshot,
    string Detail);

public interface ISafetySnapshotStore
{
    Task SaveAsync(SafetySnapshotDocument snapshot, CancellationToken cancellationToken = default);

    Task<SafetySnapshotVerificationResult> ReadAndVerifyAsync(
        string principalSid,
        string snapshotId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<int> PruneAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}

public interface ISafetySnapshotEvidenceSource
{
    Task<SafetySnapshotEvidence> CaptureAsync(CancellationToken cancellationToken = default);
}

public sealed record SafetySnapshotEvidence(
    string? BranchCode,
    string? PosNumber,
    string? InstallationGuid,
    string? ClientName,
    string? ProductRelease,
    string? ConfigurationFingerprint,
    string? PackageVersion,
    string? PackageHash,
    IReadOnlyList<SafetySnapshotServiceEvidence> Services,
    IReadOnlyList<SafetySnapshotDatabaseEvidence> Databases,
    SafetySnapshotCapacityEvidence Capacity,
    SafetySnapshotBackupEvidence Backups,
    SafetySnapshotEvidenceState State);
