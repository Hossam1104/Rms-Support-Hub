namespace RmsSupportHub.Pos.Infrastructure.Configuration;

/// <summary>Location of the legacy WinUI config file to import non-secret settings from. Overridable
/// so tests use an isolated fixture instead of the real <c>%USERPROFILE%</c> file.</summary>
public sealed class LegacyConfigurationImporterOptions
{
    public string SourceFilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".pos_admin_tool",
        "config.json");
}
