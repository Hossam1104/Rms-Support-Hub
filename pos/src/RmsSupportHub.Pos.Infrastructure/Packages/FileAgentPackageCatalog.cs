using System.Text.Json;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Packages;

/// <summary>Reads only the Agent-owned package manifest locations; no browser path or URL is accepted.</summary>
public sealed class FileAgentPackageCatalog(AgentPackageOptions options) : IAgentPackageCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AgentPackageManifest?> GetAvailableAsync(AgentPackageOperationKind operation, CancellationToken cancellationToken = default)
    {
        options.Validate();
        if (operation == AgentPackageOperationKind.Rollback) return await ReadAsync(Path.Combine(options.PackageRoot, "rollback", "manifest.json"), cancellationToken).ConfigureAwait(false);
        return await ReadAsync(Path.Combine(options.PackageRoot, "available", "manifest.json"), cancellationToken).ConfigureAwait(false);
    }

    public async Task<AgentPackageManifest?> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        options.Validate();
        return await ReadAsync(Path.Combine(options.InstallationRoot, "manifest.json"), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<AgentPackageManifest?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) return null;
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<AgentPackageManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return null; }
    }
}
