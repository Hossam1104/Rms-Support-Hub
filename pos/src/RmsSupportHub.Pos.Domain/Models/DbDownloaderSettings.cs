namespace RmsSupportHub.Pos.Domain.Models;

public sealed class DbDownloaderSettings
{
    public string ApiUrl { get; set; } = string.Empty;

    public string RdbServerIp { get; set; } = string.Empty;

    public string RdbUsername { get; set; } = string.Empty;

    public string RdbPassword { get; set; } = string.Empty;

    public string BackupRootFolder { get; set; } = @"D:\DbBackups";

    public List<string> KnownBranchCodes { get; set; } = [];

    public int PollIntervalSeconds { get; set; } = 5;

    public int TimeoutSeconds { get; set; } = 1800;

    /// <summary>Maximum number of remote observations used to prove a ZIP is stable.</summary>
    public int StableSizeObservationAttempts { get; set; } = 3;

    /// <summary>Deterministic, bounded delay between stable-size observations.</summary>
    public int StableSizeObservationIntervalSeconds { get; set; } = 2;

    public DbDownloaderSettings Clone()
    {
        return new DbDownloaderSettings
        {
            ApiUrl = ApiUrl,
            RdbServerIp = RdbServerIp,
            RdbUsername = RdbUsername,
            RdbPassword = RdbPassword,
            BackupRootFolder = BackupRootFolder,
            KnownBranchCodes = [.. KnownBranchCodes],
            PollIntervalSeconds = PollIntervalSeconds,
            TimeoutSeconds = TimeoutSeconds,
            StableSizeObservationAttempts = StableSizeObservationAttempts,
            StableSizeObservationIntervalSeconds = StableSizeObservationIntervalSeconds
        };
    }
}
