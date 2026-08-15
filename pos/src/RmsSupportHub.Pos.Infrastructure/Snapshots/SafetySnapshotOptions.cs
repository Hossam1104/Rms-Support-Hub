namespace RmsSupportHub.Pos.Infrastructure.Snapshots;

public sealed class SafetySnapshotOptions
{
    public string EnvironmentName { get; init; } = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Unknown";

    public string? ProfileId { get; init; }

    public string RootDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "DBS",
        "RmsSupportHub.Pos.Agent",
        "snapshots");

    public int MaxSnapshots { get; init; } = 32;

    public long MaxSnapshotBytes { get; init; } = 2L * 1024 * 1024;

    public TimeSpan Lifetime { get; init; } = TimeSpan.FromHours(2);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EnvironmentName) || EnvironmentName.Length > 64 || EnvironmentName.Any(char.IsControl)) throw new ArgumentException("A bounded deployment environment is required.", nameof(EnvironmentName));
        if (ProfileId is not null && (ProfileId.Length > 64 || ProfileId.Any(char.IsControl))) throw new ArgumentException("A bounded Main Server profile identifier is required.", nameof(ProfileId));
        if (string.IsNullOrWhiteSpace(RootDirectory) || !Path.IsPathFullyQualified(RootDirectory)) throw new ArgumentException("A fixed snapshot root is required.");
        if (MaxSnapshots < 1 || MaxSnapshots > 256) throw new ArgumentOutOfRangeException(nameof(MaxSnapshots));
        if (MaxSnapshotBytes < 4096 || MaxSnapshotBytes > 16L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxSnapshotBytes));
        if (Lifetime <= TimeSpan.Zero || Lifetime > TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(Lifetime));
    }
}
