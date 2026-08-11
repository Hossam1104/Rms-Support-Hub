namespace RmsSupportHub.Pos.Contracts.V1.Restore;

/// <summary><c>POST /api/v1/restores/{previewId}/execute</c> request.</summary>
public sealed record RestoreExecuteRequestDto(
    string PreviewId,
    string TypedConfirmation,
    string? IdempotencyKey = null);
