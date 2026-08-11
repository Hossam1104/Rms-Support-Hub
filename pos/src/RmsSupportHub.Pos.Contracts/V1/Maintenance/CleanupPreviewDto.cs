namespace RmsSupportHub.Pos.Contracts.V1.Maintenance;

/// <summary>
/// <c>POST /api/v1/maintenance/cleanup/preview</c> response — the impact list plus an expiring
/// one-time <see cref="ChallengeId"/>, per the destructive-operation control set (plan section 6.3).
/// Never shows a preselected acceptance; the UI must not default the confirmation to true.
/// </summary>
public sealed record CleanupPreviewDto(
    string ChallengeId,
    IReadOnlyList<string> ServicesToStop,
    IReadOnlyList<string> PathsToDelete,
    string ConfirmationPhrase,
    DateTimeOffset ExpiresAtUtc)
{
    public bool Ready { get; init; }

    public IReadOnlyList<CleanupTargetPreviewDto> Targets { get; init; } = [];

    public IReadOnlyList<MaintenancePolicyRejectionDto> Rejections { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public long? AvailableFreeSpaceBytes { get; init; }
}
