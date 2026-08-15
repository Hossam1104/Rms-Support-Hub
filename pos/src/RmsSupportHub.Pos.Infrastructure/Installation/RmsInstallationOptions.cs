namespace RmsSupportHub.Pos.Infrastructure.Installation;

/// <summary>
/// Fixed, server-owned locations for the installed RMS+ suite. These values identify known files;
/// they are not browser input and are never returned as filesystem paths by the Agent API.
/// </summary>
public sealed class RmsInstallationOptions
{
    public string RmsInfoPath { get; set; } = @"C:\ProgramData\RMS_Plus\RMSInfo.json";

    public string ReleaseNumberPath { get; set; } = @"C:\ProgramData\RMS_Plus\ReleaseNumber.txt";

    public string BranchServerSettingsPath { get; set; } =
        @"C:\Workspaces\DBS\RMS\RMS.BranchServer\appsettings.json";

    public string CashierServerSettingsPath { get; set; } =
        @"C:\Workspaces\DBS\RMS\RMS.CashierServer\appsettings.json";

    public string CashierUiSettingsPath { get; set; } =
        @"C:\Workspaces\DBS\RMS\RMS.CashierUI\appsettings.json";

    public string ServicesManagerSettingsPath { get; set; } =
        @"C:\Workspaces\DBS\RMS\RMSServicesManager\appsettings.json";

    public string BranchServerDirectory { get; set; } =
        @"C:\Workspaces\DBS\RMS\RMS.BranchServer";

    public string CashierServerDirectory { get; set; } =
        @"C:\Workspaces\DBS\RMS\RMS.CashierServer";

    public string CashierUiDirectory { get; set; } =
        @"C:\Workspaces\DBS\RMS\RMS.CashierUI";

    public string ServicesManagerDirectory { get; set; } =
        @"C:\Workspaces\DBS\RMS\RMSServicesManager";
}
