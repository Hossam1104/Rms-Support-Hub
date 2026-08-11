namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>
/// Requested update to the non-secret configuration plus optional secret replacement. A null or
/// blank <see cref="SqlPassword"/>/<see cref="Downloader"/>.RdbPassword means "keep the current
/// secret" (plan section 5.5) — clearing is a distinct, separately authorized operation.
/// </summary>
public sealed class AgentConfigurationUpdate
{
    public string SqlInstance { get; set; } = string.Empty;

    public string SqlUser { get; set; } = string.Empty;

    public string? SqlPassword { get; set; }

    public string BranchCode { get; set; } = string.Empty;

    public string PosNumber { get; set; } = string.Empty;

    public string Release { get; set; } = string.Empty;

    public string ClientName { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } = string.Empty;

    public List<string> Databases { get; set; } = [];

    public List<string> Services { get; set; } = [];

    public AgentDownloaderConfigurationUpdate Downloader { get; set; } = new();

    public long ExpectedVersion { get; set; }
}

public sealed class AgentDownloaderConfigurationUpdate
{
    public string ApiUrl { get; set; } = string.Empty;

    public string RdbServerIp { get; set; } = string.Empty;

    public string RdbUsername { get; set; } = string.Empty;

    public string? RdbPassword { get; set; }

    public List<string> KnownBranchCodes { get; set; } = [];

    public int PollIntervalSeconds { get; set; } = 5;

    public int TimeoutSeconds { get; set; } = 1800;
}
