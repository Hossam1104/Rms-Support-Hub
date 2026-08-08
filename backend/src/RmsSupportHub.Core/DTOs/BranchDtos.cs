namespace RmsSupportHub.Core.DTOs;

/// <summary>One option in a branch picker: the exact BranchCode the payload
/// needs plus the human-readable name from dbo.Branches (the verified U3
/// branch contract). Replaces the history-derived option shape, which could
/// only see branches that already had orders.</summary>
public record BranchOptionDto(
    string Code,
    string Name
);
