namespace RmsSupportHub.Pos.Contracts.V1.Configuration;

/// <summary>
/// <see cref="RdbPassword"/> is write-only; omitted/null means "keep the current secret" (plan
/// section 5.5), matching <see cref="ConfigurationUpdateRequestDto.SqlPassword"/>.
/// </summary>
public sealed record DownloaderConfigurationUpdateRequestDto(
    string ApiUrl,
    string RdbServerIp,
    string RdbUsername,
    string? RdbPassword,
    IReadOnlyList<string> KnownBranchCodes,
    int PollIntervalSeconds,
    int TimeoutSeconds);
