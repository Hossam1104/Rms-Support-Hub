namespace RmsSupportHub.Pos.Contracts.V1.Packages;

public enum AgentPackageOperationKindDto
{
    Install,
    Upgrade,
    Uninstall,
    Rollback,
    Repair,
    Health
}

public enum AgentPackageVerificationStateDto
{
    Unverified,
    Verified,
    Rejected,
    Unknown
}

public enum AgentPackageOperationStateDto
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

public sealed record AgentPackageFileDto(
    string LogicalName,
    long SizeBytes,
    string Sha256,
    bool Required);

/// <summary>Public manifest projection. Relative machine paths and source locations remain Agent-owned.</summary>
public sealed record AgentPackageManifestDto(
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
    IReadOnlyList<AgentPackageFileDto> Files,
    IReadOnlyList<string> AclRequirements,
    IReadOnlyList<string> CertificateRequirements,
    string? PreviousVersion,
    bool RollbackAvailable);

public sealed record AgentPackageStatusDto(
    string? InstalledVersion,
    string? PreviousVersion,
    AgentPackageVerificationStateDto Verification,
    AgentPackageOperationStateDto State,
    AgentPackageManifestDto? Manifest,
    string Detail);

public sealed record AgentPackagePreviewDto(
    string PreviewId,
    AgentPackageOperationKindDto Operation,
    bool Ready,
    AgentPackageVerificationStateDto Verification,
    AgentPackageManifestDto? Manifest,
    IReadOnlyList<string> Effects,
    IReadOnlyList<string> Blockers,
    string ConfirmationPhrase,
    DateTimeOffset ExpiresAtUtc);

public sealed record AgentPackageOperationRequestDto(
    string PreviewId,
    string TypedConfirmation,
    string IdempotencyKey,
    string? SnapshotId = null);

public sealed record AgentPackageOperationDto(
    string OperationId,
    AgentPackageOperationKindDto Operation,
    AgentPackageOperationStateDto State,
    AgentPackageOperationStateDto Outcome,
    int ProgressPercent,
    string Stage,
    string Detail,
    bool RollbackAttempted,
    bool RollbackSucceeded,
    bool RecoveryRequired,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string CorrelationId);
