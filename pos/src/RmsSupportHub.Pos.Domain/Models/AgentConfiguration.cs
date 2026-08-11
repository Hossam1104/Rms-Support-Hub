namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>
/// Service-owned, non-secret Agent configuration (plan section 5.5). Deliberately separate from the
/// legacy <see cref="AppSettings"/> model: no SQL or RDB password field exists here at all, so a
/// secret can never leak into this type by accident. Secrets live only in <c>IAgentSecretStore</c>.
/// </summary>
public sealed class AgentConfiguration
{
    public string SqlInstance { get; set; } = string.Empty;

    public string SqlUser { get; set; } = string.Empty;

    public string BranchCode { get; set; } = string.Empty;

    public string PosNumber { get; set; } = string.Empty;

    public string Release { get; set; } = string.Empty;

    public string ClientName { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } = string.Empty;

    public string BackupFolder { get; set; } = string.Empty;

    // Server-owned database-file destination used by restore MOVE planning. It is never returned
    // in browser configuration DTOs; restore previews expose safe destination filenames only.
    public string DbFilesPath { get; set; } = string.Empty;

    // These are service-owned non-secret source locations. They are intentionally omitted from
    // the browser DTO; backup requests select one of the three stable component IDs instead.
    public string BranchConfigPath { get; set; } = string.Empty;

    public string CashierGrpcConfigPath { get; set; } = string.Empty;

    public string CashierUiConfigPath { get; set; } = string.Empty;

    public List<string> Databases { get; set; } = [];

    public List<string> Services { get; set; } = [];

    /// <summary>Service-owned maintenance policy; omitted from redacted browser configuration.</summary>
    public MaintenanceSettings Maintenance { get; set; } = new();

    public AgentDownloaderConfiguration Downloader { get; set; } = new();

    /// <summary>Optimistic-concurrency token. Incremented on every persisted mutation, including
    /// secret-only changes, so a client's last read always reflects whether anything changed.</summary>
    public long Version { get; set; }

    public AgentConfiguration Clone()
    {
        return new AgentConfiguration
        {
            SqlInstance = SqlInstance,
            SqlUser = SqlUser,
            BranchCode = BranchCode,
            PosNumber = PosNumber,
            Release = Release,
            ClientName = ClientName,
            ApiBaseUrl = ApiBaseUrl,
            BackupFolder = BackupFolder,
            DbFilesPath = DbFilesPath,
            BranchConfigPath = BranchConfigPath,
            CashierGrpcConfigPath = CashierGrpcConfigPath,
            CashierUiConfigPath = CashierUiConfigPath,
            Databases = Databases is null ? [] : [.. Databases],
            Services = Services is null ? [] : [.. Services],
            Maintenance = Maintenance?.Clone() ?? new MaintenanceSettings(),
            Downloader = Downloader?.Clone() ?? new AgentDownloaderConfiguration(),
            Version = Version
        };
    }
}
