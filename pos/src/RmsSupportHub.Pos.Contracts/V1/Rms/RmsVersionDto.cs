namespace RmsSupportHub.Pos.Contracts.V1.Rms;

public sealed record RmsVersionDto(
    string? BranchServerBuildNumber,
    string? CashierServerBuildNumber,
    string? CashierUiBuildNumber);
