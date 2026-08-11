using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Interfaces;

/// <summary>
/// Service-owned, non-secret configuration storage (plan section 5.5). Implementations persist
/// under a restricted-ACL directory with atomic writes; no implementation may ever store a secret.
/// </summary>
public interface IAgentConfigurationStore
{
    /// <summary>Loads the current configuration, creating and persisting a fresh default (Version 1,
    /// no credential, no environment-specific address) the first time it is called.</summary>
    Task<AgentConfiguration> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically persists <paramref name="configuration"/> exactly as given. Callers own
    /// version-conflict checking and version increment before calling this.</summary>
    Task SaveAsync(AgentConfiguration configuration, CancellationToken cancellationToken = default);
}
