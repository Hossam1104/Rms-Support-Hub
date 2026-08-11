namespace RmsSupportHub.Pos.Contracts.V1.Files;

/// <summary>
/// <c>POST /api/v1/files/browse</c> request. <see cref="RootId"/> selects a configured, allowlisted
/// browse root; <see cref="RelativeSubPath"/> is relative to that root and is canonicalized and
/// re-checked for containment after resolution (plan section 5.7). Never an absolute path.
/// </summary>
public sealed record FileBrowseRequestDto(string RootId, string RelativeSubPath);
