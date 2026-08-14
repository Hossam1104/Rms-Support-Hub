namespace RmsSupportHub.Pos.Contracts.V1.Rms;

/// <summary>Backup request; the database target is the typed route segment.</summary>
public sealed record RmsDatabaseBackupRequestDto(string IdempotencyKey);
