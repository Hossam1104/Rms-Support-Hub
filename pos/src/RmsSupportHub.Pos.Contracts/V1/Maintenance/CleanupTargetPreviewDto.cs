namespace RmsSupportHub.Pos.Contracts.V1.Maintenance;

public sealed record CleanupTargetPreviewDto(
    string TargetId,
    bool Accepted,
    bool Exists,
    bool IsDirectory,
    long? LengthBytes,
    int? ChildCount,
    string? RejectionCode);
