namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>
/// The only database targets supported by the Agent database workflow. The target IDs are stable
/// server-owned labels; they are not database names supplied by a browser.
/// </summary>
public sealed record RmsDatabaseTargetDefinition(
    RmsDatabaseKind Kind,
    string TargetId,
    string DatabaseName,
    string DisplayName,
    string ServiceName);

public static class RmsDatabaseCatalog
{
    public static IReadOnlyList<RmsDatabaseTargetDefinition> Definitions { get; } =
    [
        new(RmsDatabaseKind.Branch, "branch", "RmsBranchSrv", "Branch Database", RmsServiceCatalog.BranchServiceName),
        new(RmsDatabaseKind.Cashier, "cashier", "RmsCashierSrv", "Cashier Database", RmsServiceCatalog.CashierServiceName)
    ];

    public static bool TryResolve(string? targetId, out RmsDatabaseTargetDefinition definition)
    {
        definition = Definitions.FirstOrDefault(target =>
            string.Equals(target.TargetId, targetId, StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }

    public static RmsDatabaseTargetDefinition For(RmsDatabaseKind database) =>
        Definitions.First(target => target.Kind == database);
}

public enum RmsDatabaseSqlOutcome
{
    NotAttempted,
    Completed,
    Failed,
    OutcomeUnknown
}

public sealed record RmsDatabaseSqlBackupResult(
    RmsDatabaseSqlOutcome Outcome,
    string Code,
    string Detail);

public sealed record RmsDatabaseSqlInspectionResult(
    RmsDatabaseSqlOutcome Outcome,
    string Code,
    string Detail,
    IReadOnlyList<RestoreFileInfo> LogicalFiles);

public sealed record RmsDatabaseSqlRestoreResult(
    RmsDatabaseSqlOutcome Outcome,
    string Code,
    string Detail,
    bool RecoveryRequired,
    IReadOnlyList<string> Warnings);

/// <summary>
/// A server-owned backup allocation. The path stays inside the Agent and is never copied into a
/// transport DTO or log message.
/// </summary>
public sealed record RmsDatabaseBackupAllocation(
    string ServerPath,
    string DisplayName,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// An approved Agent artifact. ServerPath is an internal capability used only by the privileged
/// restore adapter; callers must project this record to a sanitized transport DTO.
/// </summary>
public sealed record RmsApprovedDatabaseBackup(
    RmsDatabaseKind Database,
    string ArtifactId,
    string DisplayName,
    long SizeBytes,
    string Sha256Checksum,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    string ServerPath);

public interface IRmsDatabaseSqlOperations
{
    Task<RmsDatabaseSqlBackupResult> BackupAsync(
        RmsDatabaseKind database,
        string backupPath,
        CancellationToken cancellationToken = default);

    Task<RmsDatabaseSqlInspectionResult> InspectBackupAsync(
        RmsDatabaseKind database,
        string backupPath,
        CancellationToken cancellationToken = default);

    Task<RmsDatabaseSqlRestoreResult> RestoreAsync(
        RmsDatabaseKind database,
        string backupPath,
        IReadOnlyList<RestoreFileInfo> logicalFiles,
        string databaseFilesRoot,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyDatabaseAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken = default);
}

public interface IRmsDatabaseBackupStorage
{
    Task<RmsDatabaseBackupAllocation> AllocateAsync(
        RmsDatabaseKind database,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);

    Task<RmsApprovedDatabaseBackup?> RegisterAsync(
        RmsDatabaseKind database,
        RmsDatabaseBackupAllocation allocation,
        CancellationToken cancellationToken = default);

    Task<RmsApprovedDatabaseBackup?> ResolveAsync(
        RmsDatabaseKind database,
        string artifactId,
        CancellationToken cancellationToken = default);

    IReadOnlyList<RmsApprovedDatabaseBackup> List(RmsDatabaseKind database);
}

public sealed record RmsDatabaseProgress(
    int Percent,
    string Stage,
    string Detail);

public enum RmsDatabaseWorkflowOutcome
{
    NotAttempted,
    Completed,
    Failed,
    OutcomeUnknown
}

public sealed record RmsDatabaseWorkflowResult(
    RmsDatabaseWorkflowOutcome Outcome,
    string Code,
    string Detail,
    RmsApprovedDatabaseBackup? Backup,
    bool DestructiveAttempted,
    bool RecoveryRequired,
    IReadOnlyList<string> Warnings);

public interface IRmsDatabaseWorkflow
{
    Task<RmsDatabaseWorkflowResult> BackupAsync(
        RmsDatabaseKind database,
        IProgress<RmsDatabaseProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<RmsDatabaseWorkflowResult> RestoreAsync(
        RmsDatabaseKind database,
        string artifactId,
        IProgress<RmsDatabaseProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public enum RmsPrivilegedAuditEventKind
{
    Requested,
    Accepted,
    Started,
    ServiceCoordination,
    Dispatch,
    Completed,
    Failed,
    OutcomeUnknown
}

public sealed record RmsPrivilegedAuditEvent(
    DateTimeOffset AtUtc,
    RmsPrivilegedAuditEventKind Kind,
    RmsDatabaseKind Database,
    string Operation,
    string CorrelationId,
    string PrincipalSid,
    string Detail);
