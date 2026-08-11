namespace RmsSupportHub.Pos.Contracts.V1.Restore;

/// <summary>
/// Optional metadata for streamed upload clients. The file bytes are sent as the request body;
/// clients cannot provide a host path or destination.
/// </summary>
public sealed record RestoreUploadRequestDto(string FileName);
