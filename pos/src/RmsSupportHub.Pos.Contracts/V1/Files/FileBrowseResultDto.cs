namespace RmsSupportHub.Pos.Contracts.V1.Files;

/// <summary><c>POST /api/v1/files/browse</c> response.</summary>
public sealed record FileBrowseResultDto(
    string RootId,
    string RelativeSubPath,
    IReadOnlyList<FileBrowseEntryDto> Entries);
