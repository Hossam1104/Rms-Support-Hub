namespace RmsSupportHub.Pos.Contracts.V1.Files;

/// <summary>
/// One directory entry. <see cref="RelativeSubPath"/> is relative to the browsed root and is what a
/// follow-up browse or handle request must send back — never an absolute path (plan section 5.7).
/// </summary>
public sealed record FileBrowseEntryDto(
    string Name,
    bool IsDirectory,
    string RelativeSubPath,
    long? SizeBytes,
    DateTimeOffset? LastModifiedUtc);
