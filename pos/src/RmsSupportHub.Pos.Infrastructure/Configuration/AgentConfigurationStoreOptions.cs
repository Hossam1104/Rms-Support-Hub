namespace RmsSupportHub.Pos.Infrastructure.Configuration;

/// <summary>Root directory for the Agent's service-owned configuration and secret files (plan
/// section 5.5). Overridable so tests use an isolated temporary directory instead of the real
/// machine-wide <c>%ProgramData%</c>.</summary>
public sealed class AgentConfigurationStoreOptions
{
    public string RootDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "DBS",
        "PosAdminTool");
}
