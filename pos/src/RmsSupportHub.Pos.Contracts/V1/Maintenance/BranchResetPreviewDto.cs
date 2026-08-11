namespace RmsSupportHub.Pos.Contracts.V1.Maintenance;

/// <summary><c>POST /api/v1/maintenance/reset/preview</c> response — same control set as cleanup (plan section 6.3).</summary>
public sealed record BranchResetPreviewDto(
    string ChallengeId,
    string BranchCode,
    IReadOnlyList<string> AffectedTables,
    string ConfirmationPhrase,
    DateTimeOffset ExpiresAtUtc)
{
    public bool Ready { get; init; }

    public string DatabaseName { get; init; } = string.Empty;

    public IReadOnlyList<BranchResetTablePreviewDto> TableScopes { get; init; } = [];

    public IReadOnlyList<MaintenancePolicyRejectionDto> Rejections { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public long? AvailableFreeSpaceBytes { get; init; }
}
