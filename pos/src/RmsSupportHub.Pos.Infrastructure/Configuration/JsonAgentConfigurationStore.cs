using System.Text.Json;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Configuration;

/// <summary>
/// Service-owned, non-secret configuration store (plan section 5.5). Persists a single JSON file
/// under <see cref="AgentConfigurationStoreOptions.RootDirectory"/>, ACL-restricted by
/// <see cref="ServiceOwnedDirectoryProvisioner"/>, with atomic writes via <see cref="AtomicFileWriter"/>.
/// </summary>
public sealed class JsonAgentConfigurationStore(AgentConfigurationStoreOptions options) : IAgentConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _rootDirectory = options.RootDirectory;

    private string ConfigFilePath => Path.Combine(_rootDirectory, "configuration.json");

    public async Task<AgentConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(_rootDirectory);

            if (!File.Exists(ConfigFilePath))
            {
                var fresh = new AgentConfiguration { Version = 1 };
                await AtomicFileWriter.WriteAsync(ConfigFilePath, JsonSerializer.Serialize(fresh, JsonOptions), cancellationToken)
                    .ConfigureAwait(false);
                return fresh.Clone();
            }

            var json = await File.ReadAllTextAsync(ConfigFilePath, cancellationToken).ConfigureAwait(false);
            var loaded = JsonSerializer.Deserialize<AgentConfiguration>(json, JsonOptions) ?? new AgentConfiguration { Version = 1 };
            return loaded.Clone();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(AgentConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(_rootDirectory);
            var json = JsonSerializer.Serialize(configuration, JsonOptions);
            await AtomicFileWriter.WriteAsync(ConfigFilePath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }
}
