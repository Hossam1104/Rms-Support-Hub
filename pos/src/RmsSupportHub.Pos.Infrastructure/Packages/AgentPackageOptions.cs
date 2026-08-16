using RmsSupportHub.Pos.Infrastructure.Configuration;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Packages;

public sealed class AgentPackageOptions
{
    public string PackageRoot { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "DBS",
        AgentProductIdentity.ProductId,
        "packages");

    public string InstallationRoot { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "DBS",
        AgentProductIdentity.ProductId);

    public string ServiceName { get; init; } = AgentProductIdentity.PermanentServiceName;

    public string ProductId { get; init; } = AgentProductIdentity.ProductId;

    public string ReleaseChannel { get; init; } = AgentProductIdentity.ReleaseChannelProduction;

    public long MaxPackageBytes { get; init; } = 256L * 1024 * 1024;

    public string AvailableRoot => Path.Combine(PackageRoot, "available");

    public string RollbackRoot => Path.Combine(PackageRoot, "rollback");

    public string StagingRoot => Path.Combine(PackageRoot, "staging");

    public string LifecycleStatePath => Path.Combine(PackageRoot, "lifecycle-state.json");

    public string RollbackPayloadRoot => Path.Combine(RollbackRoot, "payload");

    public void Validate()
    {
        ValidateFixedPath(PackageRoot, "A fixed package root is required.");
        ValidateFixedPath(InstallationRoot, "A fixed installation root is required.");
        if (Path.IsPathRooted(LifecycleStatePath) is false) throw new ArgumentException("The lifecycle state path must remain beneath the package root.");
        if (!string.Equals(ServiceName, AgentProductIdentity.PermanentServiceName, StringComparison.Ordinal)) throw new ArgumentException("The Agent service identity is fixed.");
        if (!string.Equals(ProductId, AgentProductIdentity.ProductId, StringComparison.Ordinal)) throw new ArgumentException("The Agent product identity is fixed.");
        if (ReleaseChannel is not (AgentProductIdentity.ReleaseChannelTesting or AgentProductIdentity.ReleaseChannelProduction)) throw new ArgumentException("The Agent release channel is invalid.");
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
