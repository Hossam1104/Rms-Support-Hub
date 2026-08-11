using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Infrastructure.Configuration;

/// <summary>
/// Service-scoped secret storage for the SQL and RDB passwords (plan section 5.5). Each secret is
/// encrypted at rest with Windows Data Protection (<see cref="DataProtectionScope.LocalMachine"/>,
/// so the service identity can decrypt it without a loaded user profile) and the backing file lives
/// in the same ACL-restricted directory as the non-secret configuration. Machine-scope DPAPI and
/// the constant entropy are domain separation, not a promise that every other process on the
/// machine is cryptographically excluded; the service-owned filesystem ACL remains load-bearing.
/// </summary>
public sealed class DpapiAgentSecretStore(AgentConfigurationStoreOptions options) : IAgentSecretStore
{
    // Domain-separation entropy for DPAPI, not a credential: DPAPI's actual protection comes from
    // the OS-managed machine key, and this value must simply match between Protect and Unprotect.
    private static readonly byte[] Entropy = "PosAdminTool.Agent.Secrets.v1"u8.ToArray();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _rootDirectory = options.RootDirectory;

    private string SecretsFilePath => Path.Combine(_rootDirectory, "secrets.dat");

    public async Task<bool> HasSecretAsync(AgentSecretKind kind, CancellationToken cancellationToken = default)
    {
        var value = await TryGetSecretAsync(kind, cancellationToken).ConfigureAwait(false);
        return !string.IsNullOrEmpty(value);
    }

    public async Task<string?> TryGetSecretAsync(AgentSecretKind kind, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
            return Select(state, kind);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetSecretAsync(AgentSecretKind kind, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        await MutateAsync(kind, value, cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearSecretAsync(AgentSecretKind kind, CancellationToken cancellationToken = default)
    {
        await MutateAsync(kind, null, cancellationToken).ConfigureAwait(false);
    }

    private async Task MutateAsync(AgentSecretKind kind, string? value, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
            state = kind switch
            {
                AgentSecretKind.SqlPassword => state with { SqlPassword = value },
                AgentSecretKind.RdbPassword => state with { RdbPassword = value },
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };

            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(_rootDirectory);
            var record = new StoredSecrets
            {
                SqlPassword = Protect(state.SqlPassword),
                RdbPassword = Protect(state.RdbPassword)
            };
            var json = JsonSerializer.Serialize(record, JsonOptions);
            await AtomicFileWriter.WriteAsync(SecretsFilePath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<SecretState> LoadStateAsync(CancellationToken cancellationToken)
    {
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(_rootDirectory);
        if (!File.Exists(SecretsFilePath))
        {
            return new SecretState(null, null);
        }

        var json = await File.ReadAllTextAsync(SecretsFilePath, cancellationToken).ConfigureAwait(false);
        var record = JsonSerializer.Deserialize<StoredSecrets>(json, JsonOptions) ?? new StoredSecrets();
        return new SecretState(Unprotect(record.SqlPassword), Unprotect(record.RdbPassword));
    }

    private static string? Select(SecretState state, AgentSecretKind kind) => kind switch
    {
        AgentSecretKind.SqlPassword => state.SqlPassword,
        AgentSecretKind.RdbPassword => state.RdbPassword,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static string? Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return null;
        }

        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext), Entropy, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string? Unprotect(string? storedBase64)
    {
        if (string.IsNullOrEmpty(storedBase64))
        {
            return null;
        }

        try
        {
            var plainBytes = ProtectedData.Unprotect(Convert.FromBase64String(storedBase64), Entropy, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    private sealed record SecretState(string? SqlPassword, string? RdbPassword);

    private sealed class StoredSecrets
    {
        public string? SqlPassword { get; set; }

        public string? RdbPassword { get; set; }
    }
}
