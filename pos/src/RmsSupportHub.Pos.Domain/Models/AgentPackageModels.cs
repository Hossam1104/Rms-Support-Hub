namespace RmsSupportHub.Pos.Domain.Models;

public enum AgentPackageOperationKind
{
    Install,
    Upgrade,
    Uninstall,
    Rollback,
    Repair,
    Health
}

public enum AgentPackageVerificationState
{
    Unverified,
    Verified,
    Rejected,
    Unknown
}

public enum AgentPackageOperationState
{
    NotAttempted,
    Busy,
    Preview,
    Accepted,
    Staging,
    Activating,
    Verifying,
    Completed,
    Failed,
    RollbackSucceeded,
    RollbackFailed,
    RecoveryRequired,
    OutcomeUnknown
}

public sealed record AgentPackageFileManifest(
    string LogicalName,
    string RelativePath,
    long SizeBytes,
    string Sha256,
    bool Required);

/// <summary>Agent-owned package manifest. RelativePath is never serialized to the browser projection.</summary>
public sealed record AgentPackageManifest(
    string PackageId,
    string Version,
    string SupportedOperatingSystem,
    string SupportedRuntime,
    string ServiceDisplayName,
    string ServiceIdentity,
    string ScmName,
    string SignatureAlgorithm,
    string SignerDisplayName,
    string PackageSha256,
    string Signature,
    IReadOnlyList<AgentPackageFileManifest> Files,
    IReadOnlyList<string> AclRequirements,
    IReadOnlyList<string> CertificateRequirements,
    string? PreviousVersion,
    bool RollbackAvailable,
    long PackageSizeBytes,
    int SchemaVersion = 1,
    string? ProductId = null,
    string? Architecture = null,
    string? ReleaseChannel = null,
    string? Environment = null,
    string? ServiceDescription = null);

public sealed record AgentPackageValidationResult(
    AgentPackageVerificationState State,
    IReadOnlyList<string> Blockers,
    string Detail);

public sealed record AgentPackageExecutionRequest(
    AgentPackageOperationKind Operation,
    AgentPackageManifest Manifest,
    string? SnapshotId,
    string PrincipalSid,
    string CorrelationId);

public sealed record AgentPackageExecutionResult(
    AgentPackageOperationState State,
    bool RollbackAttempted,
    bool RollbackSucceeded,
    bool RecoveryRequired,
    string Detail);

public enum AgentPackageLifecyclePhase
{
    None,
    Prepared,
    Staged,
    PreviousPreserved,
    Activated,
    HealthChecking,
    Committed,
    RollingBack,
    RecoveryRequired
}

/// <summary>
/// Durable, server-owned lifecycle checkpoint. Paths are intentionally not stored: every
/// location is derived from fixed Agent package options and the operation identity.
/// </summary>
public sealed record AgentPackageLifecycleState(
    string OperationId,
    AgentPackageOperationKind Operation,
    AgentPackageLifecyclePhase Phase,
    string PackageId,
    string PackageVersion,
    string? PreviousVersion,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? FailureCode,
    bool RecoveryRequired);

public interface IAgentPackageCatalog
{
    Task<AgentPackageManifest?> GetAvailableAsync(
        AgentPackageOperationKind operation,
        CancellationToken cancellationToken = default);

    Task<AgentPackageManifest?> GetInstalledAsync(CancellationToken cancellationToken = default);
}

public interface IAgentPackageVerifier
{
    Task<AgentPackageValidationResult> VerifyAsync(
        AgentPackageManifest manifest,
        CancellationToken cancellationToken = default);

    Task<AgentPackageValidationResult> VerifyInstalledAsync(
        AgentPackageManifest manifest,
        CancellationToken cancellationToken = default);

    Task<AgentPackageValidationResult> VerifyRollbackAsync(
        AgentPackageManifest manifest,
        CancellationToken cancellationToken = default) => VerifyAsync(manifest, cancellationToken);
}

public interface IAgentPackageLifecycle
{
    Task<AgentPackageExecutionResult> ExecuteAsync(
        AgentPackageExecutionRequest request,
        CancellationToken cancellationToken = default);
}
