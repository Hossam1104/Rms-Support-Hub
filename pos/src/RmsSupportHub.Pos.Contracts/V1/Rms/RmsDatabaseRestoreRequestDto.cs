namespace RmsSupportHub.Pos.Contracts.V1.Rms;

/// <summary>
/// Restore request. The artifact ID is an opaque handle from the Agent-approved backup catalog;
/// the confirmation phrase is fixed server truth and is not a path or SQL command.
/// </summary>
public sealed record RmsDatabaseRestoreRequestDto(
    string BackupArtifactId,
    string ConfirmationText,
    string IdempotencyKey);
