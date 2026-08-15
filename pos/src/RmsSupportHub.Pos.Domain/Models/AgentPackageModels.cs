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
    long PackageSizeBytes);

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
}

public interface IAgentPackageLifecycle
{
    Task<AgentPackageExecutionResult> ExecuteAsync(
        AgentPackageExecutionRequest request,
        CancellationToken cancellationToken = default);
}
