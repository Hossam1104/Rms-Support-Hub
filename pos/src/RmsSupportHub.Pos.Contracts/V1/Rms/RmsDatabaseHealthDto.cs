using RmsSupportHub.Pos.Contracts.V1.Common;

namespace RmsSupportHub.Pos.Contracts.V1.Rms;

/// <summary>Bounded backup and capacity evidence for one canonical RMS database.</summary>
public sealed record RmsDatabaseHealthDto(
    /// <summary>Approved backup inventory and freshness state.</summary>
    RmsDatabaseBackupHealthDto Backups,
    /// <summary>Fixed-root storage capacity evidence without exposing the root path.</summary>
    RmsStorageHealthDto Storage);

/// <summary>Approved backup count and latest-backup freshness evidence.</summary>
public sealed record RmsDatabaseBackupHealthDto(
    /// <summary>Number of physically valid, Agent-approved backups.</summary>
    int Count,
    /// <summary>UTC creation time of the newest approved backup, when available.</summary>
    DateTimeOffset? LatestCreatedAtUtc,
    /// <summary>Freshness classification of the newest approved backup.</summary>
    FreshnessState Freshness,
    /// <summary>Health classification of the backup inventory.</summary>
    HealthState State,
    /// <summary>Safe backup inventory summary.</summary>
    string Summary);

/// <summary>Fixed backup-root availability and free-space evidence.</summary>
public sealed record RmsStorageHealthDto(
    /// <summary>Health classification of the approved storage root.</summary>
    HealthState State,
    /// <summary>Available bytes reported by the approved storage provider, when available.</summary>
    long? AvailableFreeSpaceBytes,
    /// <summary>Whether the approved storage root was available as a directory.</summary>
    bool RootAvailable,
    /// <summary>Freshness of the capacity observation.</summary>
    FreshnessState Freshness,
    /// <summary>Safe capacity summary.</summary>
    string Summary);
