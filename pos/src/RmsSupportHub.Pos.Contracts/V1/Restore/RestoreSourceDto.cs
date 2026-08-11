namespace RmsSupportHub.Pos.Contracts.V1.Restore;

/// <summary>
/// Exactly one of <see cref="UploadId"/> or <see cref="BrowseHandle"/> must be set — the two
/// distinct source mechanisms from plan section 5.7/8.7 (streamed upload for a small config file,
/// or an opaque browse handle for a multi-gigabyte <c>.bak</c> already on the device). Never a
/// free-text host path.
/// </summary>
public sealed record RestoreSourceDto(string? UploadId, string? BrowseHandle);
