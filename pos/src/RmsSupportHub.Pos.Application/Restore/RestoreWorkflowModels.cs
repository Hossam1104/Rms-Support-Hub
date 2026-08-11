using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Restore;

public enum RestoreMode
{
    Full,
    DatabaseOnly,
    ConfigOnly,
}

public enum RestoreSourceKind
{
    Upload,
    BrowseHandle,
}

/// <summary>Server-only source reference. It contains no browser-supplied host path.</summary>
public sealed record RestoreSourceReference(
    RestoreSourceKind Kind,
    string? BrowseRootId,
    string? RelativeSubPath,
    string? UploadId,
    string DisplayName);

/// <summary>Resolved source data held only inside the Agent/application boundary.</summary>
public sealed record RestoreSourceDescriptor(
    RestoreSourceReference Reference,
    string CanonicalPath);

public sealed record RestoreSourceIdentity(
    string CanonicalPath,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc,
    string Sha256Checksum);

public sealed record RestoreConfigTarget(
    string TargetId,
    string ArchiveEntryName,
    string DestinationPath,
    string DestinationFileName,
    string StagedSourcePath);

public sealed record RestoreServiceTarget(
    string ServiceId,
    string ServiceName);

/// <summary>
/// Immutable server-owned destructive intent. It is the value hashed into the one-use challenge;
/// client preview fields are never used as an execution authority.
/// </summary>
public sealed record RestorePreviewIntent(
    RestoreSourceReference Source,
    RestoreSourceIdentity SourceIdentity,
    RestoreMode Mode,
    string BranchCode,
    string TargetDatabase,
    string ArchiveVersion,
    IReadOnlyList<RestoreFileInfo> LogicalFiles,
    RestoreSqlPlan SqlPlan,
    IReadOnlyList<RestoreConfigTarget> ConfigTargets,
    IReadOnlyList<RestoreServiceTarget> Services,
    long RequiredFreeSpaceBytes,
    string ConfirmationText,
    string Fingerprint);

public sealed record RestorePreviewBuildResult(
    bool Ready,
    string? ErrorCode,
    string? SafeMessage,
    RestorePreviewIntent? Intent,
    IReadOnlyList<string> Warnings);

public sealed record RestorePreparedPlan(
    RestorePreviewIntent Intent,
    string? StagingDirectory,
    string? StagedBackupPath,
    IReadOnlyList<string> Warnings);

public sealed record RestoreExecutionResult(
    OperationResult Operation,
    string? FailureCode = null);

/// <summary>Stable, sanitized restore outcome codes shared by the application and Agent worker.</summary>
public static class RestoreFailureCodes
{
    public const string SqlInspectionFailed = "restore.sql_inspection_failed";
    public const string DatabaseRestoreInterrupted = "restore.database_restore_interrupted";
    public const string BranchEvidenceMissing = "restore.branch_evidence_missing";
    public const string PartialFailure = "restore.partial_failure";
    public const string ConfigRollbackFailed = "restore.config_rollback_failed";
    public const string CancelledAfterDestructiveWork = "restore.cancelled_after_destructive_work";
    public const string ConfigCopyFailed = "restore.config_copy_failed";
    public const string ServiceRestartFailed = "restore.service_restart_failed";

    public const string DatabaseRestoreInterruptedMessage =
        "Database restore execution was interrupted after destructive work began. Database state must be verified before further operations.";
}

public sealed record RestoreArchiveLimits
{
    public const string PolicyVersion = "pos-m02-restore-policy-v1";

    public int MaxArchiveEntryCount { get; init; } = 64;

    public long MaxArchiveBytes { get; init; } = 256L * 1024 * 1024;

    public long MaxExpandedBytes { get; init; } = 512L * 1024 * 1024;

    public double MaxCompressionRatio { get; init; } = 100d;

    public long MaxManifestBytes { get; init; } = 256L * 1024;

    public int MaxLogicalFiles { get; init; } = 32;

    public static RestoreArchiveLimits Default { get; } = new();

    public void Validate()
    {
        if (MaxArchiveEntryCount < 1) throw new ArgumentOutOfRangeException(nameof(MaxArchiveEntryCount));
        if (MaxArchiveBytes < 1) throw new ArgumentOutOfRangeException(nameof(MaxArchiveBytes));
        if (MaxExpandedBytes < 1) throw new ArgumentOutOfRangeException(nameof(MaxExpandedBytes));
        if (MaxCompressionRatio <= 0 || double.IsNaN(MaxCompressionRatio) || double.IsInfinity(MaxCompressionRatio))
            throw new ArgumentOutOfRangeException(nameof(MaxCompressionRatio));
        if (MaxManifestBytes < 1) throw new ArgumentOutOfRangeException(nameof(MaxManifestBytes));
        if (MaxLogicalFiles < 1) throw new ArgumentOutOfRangeException(nameof(MaxLogicalFiles));
    }
}
