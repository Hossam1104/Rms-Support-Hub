namespace RmsSupportHub.Pos.Contracts.V1.Backups;

/// <summary>Server-owned backup choices. The browser receives labels and identities, never source paths.</summary>
public sealed record BackupOptionsDto(
    string BranchCode,
    string TargetDatabase,
    IReadOnlyList<BackupComponentDto> Components);
