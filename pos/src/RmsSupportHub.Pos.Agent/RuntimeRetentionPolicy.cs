namespace RmsSupportHub.Pos.Agent;

/// <summary>
/// Bounded retention values for short-lived Agent state. Feature sessions may consume these values
/// when their own operation and artifact stores are composed; INT-04 does not register feature
/// workers or endpoints.
/// </summary>
public sealed record RuntimeRetentionPolicy
{
    public int MaxCompletedOperations { get; init; } = 64;

    public int MaxActivityEntries { get; init; } = 64;

    public TimeSpan CompletedOperationLifetime { get; init; } = TimeSpan.FromHours(1);

    public int MaxEventsPerOperation { get; init; } = 32;

    public int MaxResultArtifactIdsPerOperation { get; init; } = 16;

    public int MaxArtifacts { get; init; } = 64;

    public TimeSpan ArtifactLifetime { get; init; } = TimeSpan.FromHours(24);

    public int MaxFileHandles { get; init; } = 256;

    public TimeSpan FileHandleLifetime { get; init; } = TimeSpan.FromMinutes(5);

    public int MaxRestoreUploads { get; init; } = 32;

    public long MaxRestoreStagedBytes { get; init; } = 128L * 1024 * 1024;

    public long MaxRestoreUploadBytes { get; init; } = 64L * 1024 * 1024;

    public TimeSpan RestoreUploadLifetime { get; init; } = TimeSpan.FromMinutes(15);

    public int MaxRestoreChallenges { get; init; } = 256;

    public TimeSpan RestoreChallengeLifetime { get; init; } = TimeSpan.FromMinutes(5);

    public int MaxMaintenanceChallenges { get; init; } = 256;

    public TimeSpan MaintenanceChallengeLifetime { get; init; } = TimeSpan.FromMinutes(5);

    public static RuntimeRetentionPolicy Default { get; } = new();

    public void Validate()
    {
        if (MaxCompletedOperations < 1) throw new ArgumentOutOfRangeException(nameof(MaxCompletedOperations));
        if (MaxActivityEntries < 1) throw new ArgumentOutOfRangeException(nameof(MaxActivityEntries));
        if (CompletedOperationLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(CompletedOperationLifetime));
        if (MaxEventsPerOperation < 3) throw new ArgumentOutOfRangeException(nameof(MaxEventsPerOperation));
        if (MaxResultArtifactIdsPerOperation < 1) throw new ArgumentOutOfRangeException(nameof(MaxResultArtifactIdsPerOperation));
        if (MaxArtifacts < 1) throw new ArgumentOutOfRangeException(nameof(MaxArtifacts));
        if (ArtifactLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ArtifactLifetime));
        if (MaxFileHandles < 1) throw new ArgumentOutOfRangeException(nameof(MaxFileHandles));
        if (FileHandleLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(FileHandleLifetime));
        if (MaxRestoreUploads < 1) throw new ArgumentOutOfRangeException(nameof(MaxRestoreUploads));
        if (MaxRestoreStagedBytes < 1) throw new ArgumentOutOfRangeException(nameof(MaxRestoreStagedBytes));
        if (MaxRestoreUploadBytes < 1 || MaxRestoreUploadBytes > MaxRestoreStagedBytes) throw new ArgumentOutOfRangeException(nameof(MaxRestoreUploadBytes));
        if (RestoreUploadLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(RestoreUploadLifetime));
        if (MaxRestoreChallenges < 1) throw new ArgumentOutOfRangeException(nameof(MaxRestoreChallenges));
        if (RestoreChallengeLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(RestoreChallengeLifetime));
        if (MaxMaintenanceChallenges < 1) throw new ArgumentOutOfRangeException(nameof(MaxMaintenanceChallenges));
        if (MaintenanceChallengeLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(MaintenanceChallengeLifetime));
    }
}
