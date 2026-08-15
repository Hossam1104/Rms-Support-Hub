namespace RmsSupportHub.Pos.Infrastructure.Packages;

public sealed class AgentPackageOptions
{
    public string PackageRoot { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "DBS",
        "RmsSupportHub.Pos.Agent",
        "packages");

    public string InstallationRoot { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "DBS",
        "RmsSupportHub.Pos.Agent");

    public string ServiceName { get; init; } = "RmsSupportHub.Pos.Agent";

    public long MaxPackageBytes { get; init; } = 256L * 1024 * 1024;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PackageRoot) || !Path.IsPathFullyQualified(PackageRoot)) throw new ArgumentException("A fixed package root is required.");
        if (string.IsNullOrWhiteSpace(InstallationRoot) || !Path.IsPathFullyQualified(InstallationRoot)) throw new ArgumentException("A fixed installation root is required.");
        if (!string.Equals(ServiceName, "RmsSupportHub.Pos.Agent", StringComparison.Ordinal)) throw new ArgumentException("The Agent service identity is fixed.");
        if (MaxPackageBytes < 1 || MaxPackageBytes > 512L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxPackageBytes));
    }
}
