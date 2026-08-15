namespace RmsSupportHub.Pos.Domain.Models;

public enum RmsFixedRootState
{
    Healthy,
    Missing,
    Inaccessible,
    Stale,
    Unknown
}

public sealed record RmsFixedRootDefinition(
    string RootId,
    string DisplayName,
    string RootPath,
    bool AggregateOnly);

public sealed record RmsFixedRootHealth(
    string RootId,
    string DisplayName,
    RmsFixedRootState State,
    bool Exists,
    bool Accessible,
    int FileCount,
    long TotalBytes,
    DateTimeOffset? OldestFileUtc,
    DateTimeOffset? NewestFileUtc,
    long? FreeBytes,
    long? TotalCapacityBytes,
    string Detail);

public sealed record RmsUpdateHealth(
    RmsFixedRootHealth SetupRoot,
    RmsFixedRootHealth DownloadsRoot,
    RmsFixedRootHealth ReleaseRepositoryRoot,
    string? ProductRelease,
    bool ReleaseFileAvailable,
    string PackageState,
    string Detail);

public sealed record RmsInsuranceAttachmentHealth(
    RmsFixedRootHealth Root,
    int AttachmentCount,
    long TotalBytes,
    DateTimeOffset? OldestAttachmentUtc,
    DateTimeOffset? NewestAttachmentUtc,
    string Detail);

public interface IRmsFixedHealthReader
{
    Task<IReadOnlyList<RmsFixedRootHealth>> ReadRootsAsync(CancellationToken cancellationToken = default);

    Task<RmsUpdateHealth> ReadUpdateHealthAsync(CancellationToken cancellationToken = default);

    Task<RmsInsuranceAttachmentHealth> ReadInsuranceAttachmentHealthAsync(CancellationToken cancellationToken = default);
}
