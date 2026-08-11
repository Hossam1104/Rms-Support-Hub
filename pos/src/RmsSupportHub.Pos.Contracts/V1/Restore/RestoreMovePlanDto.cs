namespace RmsSupportHub.Pos.Contracts.V1.Restore;

/// <summary>
/// Server-computed SQL MOVE evidence. The destination is a safe filename relative to the
/// server-owned database-file destination; a host path is never serialized.
/// </summary>
public sealed record RestoreMovePlanDto(
    string LogicalName,
    string FileType,
    string DestinationFileName);
