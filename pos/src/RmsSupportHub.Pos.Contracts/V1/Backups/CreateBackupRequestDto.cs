namespace RmsSupportHub.Pos.Contracts.V1.Backups;

/// <summary>
/// <c>POST /api/v1/backups</c> request. <see cref="DestinationHandle"/> is an opaque handle from
/// the file-browse API (plan section 5.7) — never a free-text path.
/// </summary>
public sealed record CreateBackupRequestDto(
    IReadOnlyList<string> ComponentIds,
    string DestinationHandle,
    string IdempotencyKey);
