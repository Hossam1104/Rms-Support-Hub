using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Maintenance;

public enum MaintenanceMode
{
    Cleanup,
    BranchReset,
}

public static class MaintenanceFailureCodes
{
    public const string InvalidConfiguration = "maintenance.invalid_configuration";
    public const string NoManagedRoots = "maintenance.no_managed_roots";
    public const string NoDataRoots = "maintenance.no_data_roots";
    public const string NoProtectedRoots = "maintenance.no_protected_roots";
    public const string NoInstallRoots = "maintenance.no_install_roots";
    public const string InvalidPath = "maintenance.path_invalid";
    public const string UnresolvedEnvironmentVariable = "maintenance.path_unresolved_environment_variable";
    public const string DriveRelativePath = "maintenance.path_drive_relative";
    public const string UncNotAllowed = "maintenance.path_unc_not_allowed";
    public const string OutsideManagedRoot = "maintenance.path_outside_managed_root";
    public const string ProtectedRoot = "maintenance.path_protected_root";
    public const string InstallRoot = "maintenance.path_install_root";
    public const string NotDataRoot = "maintenance.path_not_data_root";
    public const string RootTarget = "maintenance.path_root_target";
    public const string ReparsePoint = "maintenance.path_reparse_point";
    public const string ReparseEscape = "maintenance.path_reparse_escape";
    public const string ReparseInspectionFailed = "maintenance.path_reparse_inspection_failed";
    public const string PathInspectionFailed = "maintenance.path_inspection_failed";
    public const string TargetMissing = "maintenance.target_missing";
    public const string ServiceInvalid = "maintenance.service_invalid";
    public const string ServiceStopFailed = "maintenance.service_stop_failed";
    public const string ServiceStopInterrupted = "maintenance.service_stop_interrupted";
    public const string TargetDeleteFailed = "maintenance.target_delete_failed";
    public const string TargetDeleteInterrupted = "maintenance.target_delete_interrupted";
    public const string PreviewChanged = "maintenance.preview_changed";
    public const string PreviewNotReady = "maintenance.preview_not_ready";
    public const string DatabaseInvalid = "maintenance.database_invalid";
    public const string DatabaseOutOfScope = "maintenance.database_out_of_scope";
    public const string BranchInvalid = "maintenance.branch_invalid";
    public const string BranchNotFound = "maintenance.branch_not_found";
    public const string DatabaseScopeUnavailable = "maintenance.database_scope_unavailable";
    public const string SqlResetFailed = "maintenance.sql_reset_failed";
    public const string SqlResetInterrupted = "maintenance.sql_reset_interrupted";
    public const string PartialFailure = "maintenance.partial_failure";
    public const string RecoveryRequired = "maintenance.recovery_required";
    public const string CancelledBeforeDestructiveWork = "maintenance.cancelled_before_destructive_work";
    public const string OperationFailed = "maintenance.failed";

    public const string PathRejectedMessage = "The configured maintenance target was rejected by policy.";
    public const string PreviewChangedMessage = "The maintenance policy or target changed before execution.";
    public const string RecoveryGuidance = "Verify the affected service, files, or database before retrying maintenance.";
    public const string DatabaseRecoveryGuidance = "Verify branch database state before retrying the reset.";
}

public sealed record MaintenancePolicyRejection(
    string TargetId,
    string Code,
    string Message);

public sealed record MaintenancePathResolution(
    string TargetId,
    string CanonicalPath,
    string ManagedRoot,
    bool Exists,
    bool IsDirectory,
    long? LengthBytes,
    int? ChildCount);

public sealed record MaintenanceCleanupTargetPreview(
    string TargetId,
    bool Accepted,
    bool Exists,
    bool IsDirectory,
    long? LengthBytes,
    int? ChildCount,
    string? RejectionCode);

public sealed record MaintenanceTablePreview(
    string TableName,
    long? MatchingRows);

/// <summary>Immutable, server-owned evidence hashed into the one-use maintenance challenge.</summary>
public sealed record MaintenancePreviewIntent(
    MaintenanceMode Mode,
    string BranchCode,
    string DatabaseName,
    IReadOnlyList<string> TableNames,
    IReadOnlyList<string> TargetIds,
    string ConfirmationText,
    string Fingerprint);

public sealed record CleanupPreviewBuildResult(
    bool Ready,
    string? ErrorCode,
    string? SafeMessage,
    MaintenancePreviewIntent? Intent,
    IReadOnlyList<MaintenanceCleanupTargetPreview> Targets,
    IReadOnlyList<string> Services,
    IReadOnlyList<MaintenancePolicyRejection> Rejections,
    IReadOnlyList<string> Warnings,
    long? AvailableFreeSpaceBytes);

public sealed record BranchResetPreviewBuildResult(
    bool Ready,
    string? ErrorCode,
    string? SafeMessage,
    MaintenancePreviewIntent? Intent,
    IReadOnlyList<MaintenanceTablePreview> Tables,
    IReadOnlyList<string> Services,
    IReadOnlyList<MaintenancePolicyRejection> Rejections,
    IReadOnlyList<string> Warnings,
    long? AvailableFreeSpaceBytes);

public sealed record MaintenanceItemResult(
    string TargetId,
    string Kind,
    string State,
    bool Attempted,
    bool Completed,
    bool ResidueUncertain,
    string? FailureCode,
    string? RecoveryGuidance);

public sealed record MaintenanceExecutionEvidence(
    bool DestructiveAttempted,
    bool RecoveryRequired,
    IReadOnlyList<MaintenanceItemResult> Items,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> RecoveryGuidance);

public sealed record MaintenanceExecutionResult(
    OperationResult Operation,
    MaintenanceExecutionEvidence Evidence,
    string? FailureCode = null);
