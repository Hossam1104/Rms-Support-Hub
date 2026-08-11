using RmsSupportHub.Pos.Domain.Enums;

namespace RmsSupportHub.Pos.Domain.Interfaces;

/// <summary>
/// Service-scoped secure secret storage for the SQL and RDB passwords (plan section 5.5).
/// Implementations must encrypt at rest (Windows Data Protection) and restrict the backing file's
/// ACL to Administrators and the service identity. A secret value is never logged or returned
/// outside this store.
/// </summary>
public interface IAgentSecretStore
{
    Task<bool> HasSecretAsync(AgentSecretKind kind, CancellationToken cancellationToken = default);

    /// <summary>Returns the plaintext secret, or <see langword="null"/> if not set. Intended for
    /// server-side use only (e.g. building a SQL connection) — never for returning to a client.</summary>
    Task<string?> TryGetSecretAsync(AgentSecretKind kind, CancellationToken cancellationToken = default);

    Task SetSecretAsync(AgentSecretKind kind, string value, CancellationToken cancellationToken = default);

    Task ClearSecretAsync(AgentSecretKind kind, CancellationToken cancellationToken = default);
}
