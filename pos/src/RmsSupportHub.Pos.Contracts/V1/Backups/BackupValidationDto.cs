namespace RmsSupportHub.Pos.Contracts.V1.Backups;

/// <summary>Preflight evidence returned before a backup is queued.</summary>
public sealed record BackupValidationDto(
    bool Ready,
    long AvailableFreeSpaceBytes,
    long EstimatedRequiredFreeSpaceBytes,
    IReadOnlyList<string> Errors);
