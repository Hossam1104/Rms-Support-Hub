namespace RmsSupportHub.Pos.Domain.Models;

public sealed class AppSettings
{
    public string Version { get; set; } = "2.4";

    public string Theme { get; set; } = "LIGHT";

    public string SqlInstance { get; set; } = ".";

    public string SqlUser { get; set; } = "sa";

    public string SqlPassword { get; set; } = string.Empty;

    public string ClientName { get; set; } = "UPC";

    public string BranchCode { get; set; } = string.Empty;

    public string PosNumber { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } = string.Empty;

    public string BackupFolder { get; set; } = @"D:\DB Backups";

    public string DbFilesPath { get; set; } = @"D:\DB Backups";

    public string Release { get; set; } = "N/A";

    public List<string> Databases { get; set; } =
    [
        "RmsCashierSrv",
        "RmsBranchSrv"
    ];

    public List<string> Services { get; set; } =
    [
        "RMSServiceManager",
        "RMS.BranchService",
        "RMS.CashierService"
    ];

    public List<string> FoldersToDelete { get; set; } =
    [
        @"C:\Workspaces",
        @"C:\ProgramData\Logs",
        @"C:\ProgramData\Cashier",
        @"C:\ProgramData\Branch",
        @"C:\ProgramData\DBS",
        @"C:\ProgramData\RMS_Plus",
        @"C:\ProgramData\RMS_Plus_Downloads",
        @"C:\ProgramData\RMS_Plus_ReleaseRepo"
    ];

    public string BranchConfigPath { get; set; } = @"C:\Workspaces\DBS\RMS\RMS.BranchServer\appsettings.json";

    public string CashierGrpcConfigPath { get; set; } = @"C:\Workspaces\DBS\RMS\RMS.CashierServer\appsettings.json";

    public string CashierUiConfigPath { get; set; } = @"C:\Workspaces\DBS\RMS\RMS.CashierUI\appsettings.json";

    public string RmsInfoPath { get; set; } = @"C:\ProgramData\RMS_Plus\RMSInfo.json";

    public string ReleasePath { get; set; } = @"C:\ProgramData\RMS_Plus\ReleaseNumber.txt";

    /// <summary>Service-owned maintenance policy. It is never populated from a browser target.</summary>
    public MaintenanceSettings Maintenance { get; set; } = new();

    public DbDownloaderSettings DbDownloader { get; set; } = new();

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Version = Version,
            Theme = Theme,
            SqlInstance = SqlInstance,
            SqlUser = SqlUser,
            SqlPassword = SqlPassword,
            ClientName = ClientName,
            BranchCode = BranchCode,
            PosNumber = PosNumber,
            ApiBaseUrl = ApiBaseUrl,
            BackupFolder = BackupFolder,
            DbFilesPath = DbFilesPath,
            Release = Release,
            Databases = Databases is null ? [] : [.. Databases],
            Services = Services is null ? [] : [.. Services],
            FoldersToDelete = FoldersToDelete is null ? [] : [.. FoldersToDelete],
            BranchConfigPath = BranchConfigPath,
            CashierGrpcConfigPath = CashierGrpcConfigPath,
            CashierUiConfigPath = CashierUiConfigPath,
            RmsInfoPath = RmsInfoPath,
            ReleasePath = ReleasePath,
            Maintenance = Maintenance?.Clone() ?? new MaintenanceSettings(),
            DbDownloader = DbDownloader?.Clone() ?? new DbDownloaderSettings()
        };
    }
}
