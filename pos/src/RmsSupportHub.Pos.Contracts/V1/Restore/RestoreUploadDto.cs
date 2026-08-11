namespace RmsSupportHub.Pos.Contracts.V1.Restore;

/// <summary>Opaque staged upload metadata. The staging path is server-only.</summary>
public sealed record RestoreUploadDto(
    string UploadId,
    string FileName,
    long SizeBytes,
    DateTimeOffset ExpiresAtUtc);
