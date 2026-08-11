using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Infrastructure.Configuration;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

/// <summary>
/// Sentinel-only secret values throughout (AGENTS.md safety rules: never test with real
/// production credentials).
/// </summary>
public sealed class DpapiAgentSecretStoreTests : IDisposable
{
    private const string SqlSentinel = "sentinel-sql-P@ss-0001";
    private const string RdbSentinel = "sentinel-rdb-P@ss-0002";

    private readonly string _rootDirectory = Directory.CreateTempSubdirectory("pos-admin-secret-store-tests-").FullName;

    [Fact]
    public async Task FreshStore_HasNeitherSecret()
    {
        var store = new DpapiAgentSecretStore(new AgentConfigurationStoreOptions { RootDirectory = _rootDirectory });

        Assert.False(await store.HasSecretAsync(AgentSecretKind.SqlPassword));
        Assert.False(await store.HasSecretAsync(AgentSecretKind.RdbPassword));
    }

    [Fact]
    public async Task SetThenGet_RoundTripsBothSecretsIndependently()
    {
        var store = new DpapiAgentSecretStore(new AgentConfigurationStoreOptions { RootDirectory = _rootDirectory });

        await store.SetSecretAsync(AgentSecretKind.SqlPassword, SqlSentinel);
        await store.SetSecretAsync(AgentSecretKind.RdbPassword, RdbSentinel);

        Assert.True(await store.HasSecretAsync(AgentSecretKind.SqlPassword));
        Assert.True(await store.HasSecretAsync(AgentSecretKind.RdbPassword));
        Assert.Equal(SqlSentinel, await store.TryGetSecretAsync(AgentSecretKind.SqlPassword));
        Assert.Equal(RdbSentinel, await store.TryGetSecretAsync(AgentSecretKind.RdbPassword));
    }

    [Fact]
    public async Task SettingOneSecret_DoesNotDisturbTheOther()
    {
        var store = new DpapiAgentSecretStore(new AgentConfigurationStoreOptions { RootDirectory = _rootDirectory });
        await store.SetSecretAsync(AgentSecretKind.SqlPassword, SqlSentinel);

        await store.SetSecretAsync(AgentSecretKind.RdbPassword, RdbSentinel);

        Assert.Equal(SqlSentinel, await store.TryGetSecretAsync(AgentSecretKind.SqlPassword));
    }

    [Fact]
    public async Task ClearSecret_RemovesOnlyTheRequestedKind()
    {
        var store = new DpapiAgentSecretStore(new AgentConfigurationStoreOptions { RootDirectory = _rootDirectory });
        await store.SetSecretAsync(AgentSecretKind.SqlPassword, SqlSentinel);
        await store.SetSecretAsync(AgentSecretKind.RdbPassword, RdbSentinel);

        await store.ClearSecretAsync(AgentSecretKind.SqlPassword);

        Assert.False(await store.HasSecretAsync(AgentSecretKind.SqlPassword));
        Assert.Null(await store.TryGetSecretAsync(AgentSecretKind.SqlPassword));
        Assert.True(await store.HasSecretAsync(AgentSecretKind.RdbPassword));
        Assert.Equal(RdbSentinel, await store.TryGetSecretAsync(AgentSecretKind.RdbPassword));
    }

    [Fact]
    public async Task BackingFileOnDisk_NeverContainsThePlaintextSecret()
    {
        var store = new DpapiAgentSecretStore(new AgentConfigurationStoreOptions { RootDirectory = _rootDirectory });

        await store.SetSecretAsync(AgentSecretKind.SqlPassword, SqlSentinel);
        await store.SetSecretAsync(AgentSecretKind.RdbPassword, RdbSentinel);

        var raw = await File.ReadAllTextAsync(Path.Combine(_rootDirectory, "secrets.dat"));

        Assert.DoesNotContain(SqlSentinel, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(RdbSentinel, raw, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
