namespace OnlineOrderTool.Core.DTOs;

/// <summary>One option in a branch picker: the exact BranchCode the payload
/// needs plus the human-readable name from dbo.Branches (U3,
/// UI_Rework_Plan.md D6/D7). Replaces the history-derived BranchSummaryDto,
/// which could only see branches that already had orders.</summary>
public record BranchOptionDto(
    string Code,
    string Name
);
