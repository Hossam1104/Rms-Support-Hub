namespace RmsSupportHub.Pos.Contracts.V1.Restore;

/// <summary>
/// <c>POST /api/v1/restores/preview</c> response. <see cref="PreviewId"/> must be presented at
/// execute time and is re-validated and re-policy-checked there — a stale or reused preview fails
/// closed (plan section 6.3, section 9).
/// </summary>
public sealed record RestorePreviewDto(
    string PreviewId,
    RestoreMode Mode,
    string TargetDatabase,
    IReadOnlyList<string> LogicalFiles,
    IReadOnlyList<string> ConfigDestinations,
    IReadOnlyList<string> ServicesToStop,
    long RequiredFreeSpaceBytes,
    IReadOnlyList<string> Warnings,
    DateTimeOffset ExpiresAtUtc)
{
    /// <summary>Server truth; a rejected preview never receives an executable challenge.</summary>
    public bool Ready { get; init; } = true;

    /// <summary>Stable safe identifiers and filenames only; absolute host paths never cross the API.</summary>
    public IReadOnlyList<RestoreMovePlanDto> SqlMovePlan { get; init; } = [];

    /// <summary>Exact phrase the operator must type at execution time.</summary>
    public string ConfirmationText { get; init; } = string.Empty;

    public string? RejectionCode { get; init; }

    public string? RejectionReason { get; init; }
}
