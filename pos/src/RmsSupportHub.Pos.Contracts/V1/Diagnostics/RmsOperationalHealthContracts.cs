namespace RmsSupportHub.Pos.Contracts.V1.Diagnostics;

/// <summary>Safe state for one server-owned RMS evidence root.</summary>
public enum RmsFixedRootStateDto
{
    Healthy,
    Missing,
    Inaccessible,
    Stale,
    Unknown
}

/// <summary>Aggregate health for a fixed RMS directory without returning its path or filenames.</summary>
public sealed record RmsFixedRootHealthDto(
    string RootId,
    string DisplayName,
    RmsFixedRootStateDto State,
    bool Exists,
    bool Accessible,
    int FileCount,
    long TotalBytes,
    DateTimeOffset? OldestFileUtc,
    DateTimeOffset? NewestFileUtc,
    long? FreeBytes,
    long? TotalCapacityBytes,
    string Detail);

/// <summary>Bounded release/download evidence for the known RMS update roots.</summary>
public sealed record RmsUpdateHealthDto(
    RmsFixedRootHealthDto SetupRoot,
    RmsFixedRootHealthDto DownloadsRoot,
    RmsFixedRootHealthDto ReleaseRepositoryRoot,
    string? ProductRelease,
    bool ReleaseFileAvailable,
    string PackageState,
    string Detail);

/// <summary>Aggregate-only health for the sensitive insurance attachment root.</summary>
public sealed record RmsInsuranceAttachmentHealthDto(
    RmsFixedRootHealthDto Root,
    int AttachmentCount,
    long TotalBytes,
    DateTimeOffset? OldestAttachmentUtc,
    DateTimeOffset? NewestAttachmentUtc,
    string Detail);

/// <summary>Read-only RMS storage, update, and sensitive-attachment health projection.</summary>
public sealed record RmsOperationalHealthDto(
    IReadOnlyList<RmsFixedRootHealthDto> FixedRoots,
    RmsUpdateHealthDto Updates,
    RmsInsuranceAttachmentHealthDto InsuranceAttachments,
    DateTimeOffset CheckedAtUtc);
