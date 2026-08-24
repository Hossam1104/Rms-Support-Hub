using System.Text.Json;
using RmsSupportHub.Pos.Contracts.V1.Rms;

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
    bool CanExportArtifacts);

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
