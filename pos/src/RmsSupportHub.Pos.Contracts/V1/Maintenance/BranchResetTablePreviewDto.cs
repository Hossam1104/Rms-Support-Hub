namespace RmsSupportHub.Pos.Contracts.V1.Maintenance;

public sealed record BranchResetTablePreviewDto(
    string TableName,
    long? MatchingRows);
