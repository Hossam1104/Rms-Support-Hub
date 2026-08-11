namespace RmsSupportHub.Pos.Contracts.V1.Files;

/// <summary><c>POST /api/v1/files/handles</c> request — exchanges a browsed entry for an opaque handle.</summary>
public sealed record FileHandleRequestDto(string RootId, string RelativeSubPath, FileHandlePurpose Purpose);
