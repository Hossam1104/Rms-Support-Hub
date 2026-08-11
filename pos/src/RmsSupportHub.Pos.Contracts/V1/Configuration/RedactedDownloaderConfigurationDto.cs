namespace RmsSupportHub.Pos.Contracts.V1.Configuration;

/// <summary>
/// The DB Downloader's non-secret settings plus <see cref="HasRdbPassword"/>. Never carries the
/// RDB password, a UNC path, or an SMB share detail — those stay server-side (plan section 5.5,
/// section 12).
/// </summary>
public sealed record RedactedDownloaderConfigurationDto(
    string ApiUrl,
    string RdbServerIp,
    string RdbUsername,
    bool HasRdbPassword,
    IReadOnlyList<string> KnownBranchCodes,
    int PollIntervalSeconds,
    int TimeoutSeconds);
