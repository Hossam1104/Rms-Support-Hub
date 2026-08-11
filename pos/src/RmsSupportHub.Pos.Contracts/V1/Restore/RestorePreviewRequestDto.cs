namespace RmsSupportHub.Pos.Contracts.V1.Restore;

/// <summary><c>POST /api/v1/restores/preview</c> request.</summary>
public sealed record RestorePreviewRequestDto(RestoreSourceDto Source, RestoreMode Mode);
