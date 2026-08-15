using RmsSupportHub.Pos.Infrastructure.Configuration;

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

    public string AvailableRoot => Path.Combine(PackageRoot, "available");

    public string RollbackRoot => Path.Combine(PackageRoot, "rollback");

    public string StagingRoot => Path.Combine(PackageRoot, "staging");

    public void Validate()
    {
        ValidateFixedPath(PackageRoot, "A fixed package root is required.");
        ValidateFixedPath(InstallationRoot, "A fixed installation root is required.");
        if (!string.Equals(ServiceName, "RmsSupportHub.Pos.Agent", StringComparison.Ordinal)) throw new ArgumentException("The Agent service identity is fixed.");
        if (MaxPackageBytes < 1 || MaxPackageBytes > 512L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxPackageBytes));
    }

    /// <summary>
    /// Provisions every fixed package boundary before a manifest, archive, staging file, or
    /// installed manifest is opened. Each child is protected independently so it cannot inherit
    /// permissive access from a broad %ProgramData% or %ProgramFiles% parent.
    /// </summary>
    public void EnsureStorageProvisioned()
    {
        Validate();
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(PackageRoot);
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(AvailableRoot);
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(RollbackRoot);
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(StagingRoot);
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(InstallationRoot);
    }

    private static void ValidateFixedPath(string path, string message)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !Path.IsPathFullyQualified(path)
            || path.Any(char.IsControl)
            || HasParentTraversal(path)) throw new ArgumentException(message);
    }

    private static bool HasParentTraversal(string path) =>
        path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or "..");
}
