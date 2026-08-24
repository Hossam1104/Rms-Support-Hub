using System.Text.Json;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Contracts.V1.Services;

namespace RmsSupportHub.Pos.Contracts.V1.LocalIpc;

public static class LocalIpcProtocol
{
    public const int CurrentVersion = 1;

    public const string PipeName = "RmsSupportAgent.Ipc";

    public const string HealthOperation = "agent.health";

    public const string InstallationDiscoveryOperation = "rms.installation.discovery";

    public const string ServiceHealthOperation = "rms.services.health";

    public const string DatabaseHealthOperation = "rms.databases.health";

    public const string LogEvidenceOperation = "rms.logs.evidence";

    public const string SupportBundleOperation = "support.bundle.generate";

    public const string AuthorizationOperation = "agent.authorization";

    public const string BackupInventoryOperation = "rms.backups.inventory";

    public const string BackupCreateOperation = "rms.database.backup.create";

    public const string BackupStatusOperation = "rms.database.backup.status";

    public const string BackupCancelOperation = "rms.database.backup.cancel";

    public const string ArtifactExportOperation = "artifact.export.local";

    public const string ServiceControlOperation = "rms.services.control";

    public const string ServiceControlAuthorizationOperation = "rms.services.control.authorization";
}

public sealed record LocalIpcRequestEnvelope(
    int ProtocolVersion,
    string RequestId,
    string? CorrelationId,
    string Operation,
    JsonElement? Payload);

public sealed record LocalIpcResponseEnvelope(
    int ProtocolVersion,
    string RequestId,
    string CorrelationId,
    bool Success,
    JsonElement? Result,
    LocalIpcErrorDto? Error);

public sealed record LocalIpcErrorDto(string Code, string Message);

/// <summary>Safe local Agent/IPC readiness information. Hub connectivity is deliberately absent.</summary>
public sealed record LocalIpcHealthDto(
    string AgentStatus,
    string IpcStatus,
    int ProtocolVersion,
    bool HubConnectivityRequired);

public sealed record LocalIpcAuthorizationDto(
    string AuthorizationLevel,
    bool CanReadBackupInventory,
    bool CanCreateBackup,
    bool CanExportArtifacts,
    bool CanManageRmsServices = false);

/// <summary>
/// Local service-control payload. ServiceId is an opaque server-owned catalog key; no Windows
/// service name, command, executable, timeout, or native target is accepted.
/// </summary>
public sealed record LocalIpcServiceActionRequestDto(
    string ServiceId,
    ServiceActionKind Action,
    string? Confirmation,
    string IdempotencyKey,
    string MutationAuthorization);

public sealed record LocalIpcServiceActionAuthorizationRequestDto(
    string ServiceId,
    ServiceActionKind Action,
    string? Confirmation);

public sealed record LocalIpcServiceActionAuthorizationResponseDto(
    string MutationAuthorization,
    DateTimeOffset ExpiresAtUtc,
    string ServiceId,
    ServiceActionKind Action,
    string CorrelationId);

public sealed record LocalIpcServiceActionResponseDto(
    string OperationId,
    string ServiceId,
    ServiceActionKind Action,
    LocalIpcServiceActionState State,
    int ProgressPercent,
    string Stage,
    ServiceRuntimeState? ObservedState,
    string Code,
    string Detail,
    string CorrelationId,
    bool RecoveryRequired);

public enum LocalIpcServiceActionState
{
    Queued,
    Accepted,
    Running,
    Completed,
    Failed,
    OutcomeUnknown,
    Cancelled
}

public sealed record LocalIpcBackupCreateRequestDto(RmsDatabaseTarget Target);

public sealed record LocalIpcBackupOperationRequestDto(
    RmsDatabaseTarget Target,
    string OperationId);

public sealed record LocalIpcBackupInventoryDto(
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<LocalIpcBackupArtifactDto> Items);

public sealed record LocalIpcBackupArtifactDto(
    RmsDatabaseTarget Target,
    string ArtifactId,
    string DisplayName,
    long SizeBytes,
    string Sha256Checksum,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    LocalIpcBackupAvailability Availability);

public enum LocalIpcBackupAvailability
{
    Available,
    Expired,
    Missing,
    ChecksumMismatch,
    Invalid
}

public sealed record LocalIpcArtifactExportRequestDto(
    LocalIpcArtifactKind ArtifactKind,
    string ArtifactId,
    RmsDatabaseTarget? DatabaseTarget,
    string DestinationPath,
    bool OverwriteConfirmed);

public enum LocalIpcArtifactKind
{
    DatabaseBackup,
    SupportBundle
}

public sealed record LocalIpcArtifactExportResultDto(
    LocalIpcArtifactExportState State,
    string ArtifactId,
    string DisplayName,
    long SizeBytes,
    string Sha256Checksum,
    string DestinationCategory,
    string? ErrorCode);

public enum LocalIpcArtifactExportState
{
    Succeeded,
    DestinationExists,
    DestinationRejected,
    ArtifactExpired,
    ArtifactNotFound,
    ChecksumMismatch,
    Unauthorized,
    Cancelled,
    Failed
}
