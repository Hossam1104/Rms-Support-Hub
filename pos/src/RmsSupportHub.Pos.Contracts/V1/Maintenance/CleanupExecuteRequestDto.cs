namespace RmsSupportHub.Pos.Contracts.V1.Maintenance;

/// <summary>
/// <c>POST /api/v1/maintenance/cleanup/execute</c> request. The server recomputes policy against
/// current configuration at execute time and never trusts the preview's conclusions (plan section
/// 6.3, section 11 Session task list).
/// </summary>
public sealed record CleanupExecuteRequestDto(string ChallengeId, string TypedConfirmation)
{
    public string? IdempotencyKey { get; init; }
}
