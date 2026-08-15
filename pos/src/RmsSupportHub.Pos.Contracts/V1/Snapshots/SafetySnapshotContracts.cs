namespace RmsSupportHub.Pos.Contracts.V1.Snapshots;

public enum SafetySnapshotStateDto
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

public enum SafetySnapshotEvidenceStateDto
{
    Healthy,
    Warning,
    ActionRequired,
    Unknown
}

public sealed record SafetySnapshotPreviewDto(
    string SnapshotType,
    bool Ready,
    SafetySnapshotEvidenceStateDto EvidenceState,
    IReadOnlyList<string> IncludedEvidence,
    IReadOnlyList<string> ExcludedEvidence,
    IReadOnlyList<string> Blockers,
    int RetentionMinutes,
    DateTimeOffset ExpiresAtUtc);

public sealed record SafetySnapshotCaptureRequestDto(
    string TypedConfirmation,
    string IdempotencyKey);

/// <summary>Safe pre-maintenance identity and evidence projection. Raw configuration and paths are excluded.</summary>
public sealed record SafetySnapshotDto(
    string SnapshotId,
    SafetySnapshotStateDto State,
    SafetySnapshotEvidenceStateDto EvidenceState,
    string? BranchCode,
    string? PosNumber,
    string? InstallationGuid,
    string? ClientName,
    string? ProductRelease,
    string? PackageVersion,
    string? PackageHash,
    string ConfigurationFingerprint,
    IReadOnlyList<SafetySnapshotServiceEvidenceDto> Services,
    IReadOnlyList<SafetySnapshotDatabaseEvidenceDto> Databases,
    SafetySnapshotCapacityEvidenceDto Capacity,
    SafetySnapshotBackupEvidenceDto Backups,
    string CorrelationId,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string Detail);

public sealed record SafetySnapshotServiceEvidenceDto(
    string ServiceId,
    string State,
    SafetySnapshotEvidenceStateDto EvidenceState);

public sealed record SafetySnapshotDatabaseEvidenceDto(
    string DatabaseId,
    string State,
    SafetySnapshotEvidenceStateDto EvidenceState,
    string? Identity);

public sealed record SafetySnapshotCapacityEvidenceDto(
    SafetySnapshotEvidenceStateDto State,
    long? AvailableBytes,
    string Detail);

public sealed record SafetySnapshotBackupEvidenceDto(
    SafetySnapshotEvidenceStateDto State,
    int ApprovedCount,
    DateTimeOffset? LatestCreatedAtUtc,
    string Detail);

public sealed record SafetySnapshotVerificationDto(
    string SnapshotId,
    SafetySnapshotStateDto State,
    bool Verified,
    string Detail,
    DateTimeOffset CheckedAtUtc,
    string? CorrelationId);
