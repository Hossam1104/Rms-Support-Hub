using RmsSupportHub.Pos.Application.UseCases;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Exceptions;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

/// <summary>
/// Sentinel-only secret values throughout (AGENTS.md safety rules: never test with real
/// production credentials).
/// </summary>
public sealed class AgentConfigurationUseCaseTests
{
    private const string SqlSentinel = "sentinel-sql-pw";
    private const string RdbSentinel = "sentinel-rdb-pw";
    private const long InitialVersion = 1;

    [Fact]
    public async Task GetAsync_OnAFreshEnvironment_ReportsNeitherSecretPresent()
    {
        var useCase = CreateUseCase(out _, out _);

        var snapshot = await useCase.GetAsync();

        Assert.False(snapshot.HasSqlPassword);
        Assert.False(snapshot.HasRdbPassword);
    }

    [Fact]
    public async Task UpdateAsync_WithSecretsProvided_SetsBothAndNeverExposesTheValue()
    {
        var useCase = CreateUseCase(out _, out var secretStore);
        var update = NewUpdate(InitialVersion, sqlPassword: SqlSentinel, rdbPassword: RdbSentinel);

        var snapshot = await useCase.UpdateAsync(update);

        Assert.True(snapshot.HasSqlPassword);
        Assert.True(snapshot.HasRdbPassword);
        Assert.Equal(SqlSentinel, await secretStore.TryGetSecretAsync(AgentSecretKind.SqlPassword));
        Assert.Equal(RdbSentinel, await secretStore.TryGetSecretAsync(AgentSecretKind.RdbPassword));
    }

    [Fact]
    public async Task UpdateAsync_WithBlankSecretFields_KeepsTheExistingSecrets()
    {
        var useCase = CreateUseCase(out _, out var secretStore);
        var first = await useCase.UpdateAsync(NewUpdate(InitialVersion, sqlPassword: SqlSentinel, rdbPassword: RdbSentinel));

        var second = await useCase.UpdateAsync(NewUpdate(first.Configuration.Version, sqlPassword: null, rdbPassword: null));

        Assert.True(second.HasSqlPassword);
        Assert.True(second.HasRdbPassword);
        Assert.Equal(SqlSentinel, await secretStore.TryGetSecretAsync(AgentSecretKind.SqlPassword));
        Assert.Equal(RdbSentinel, await secretStore.TryGetSecretAsync(AgentSecretKind.RdbPassword));
    }

    [Fact]
    public async Task UpdateAsync_PersistsNonSecretFields()
    {
        var useCase = CreateUseCase(out var configStore, out _);
        var update = NewUpdate(InitialVersion);
        update.SqlInstance = "SQLEXPRESS";
        update.BranchCode = "P087";

        await useCase.UpdateAsync(update);

        var persisted = await configStore.LoadAsync();
        Assert.Equal("SQLEXPRESS", persisted.SqlInstance);
        Assert.Equal("P087", persisted.BranchCode);
        Assert.Equal(InitialVersion + 1, persisted.Version);
    }

    [Fact]
    public async Task UpdateAsync_WithStaleExpectedVersion_ThrowsAndDoesNotMutateStoredState()
    {
        var useCase = CreateUseCase(out var configStore, out _);
        var firstUpdate = NewUpdate(InitialVersion);
        firstUpdate.SqlInstance = "FIRST";
        await useCase.UpdateAsync(firstUpdate);

        var staleUpdate = NewUpdate(expectedVersion: 0);
        staleUpdate.SqlInstance = "SHOULD-NOT-BE-SAVED";
        await Assert.ThrowsAsync<ConfigurationVersionConflictException>(() => useCase.UpdateAsync(staleUpdate));

        var persisted = await configStore.LoadAsync();
        Assert.Equal("FIRST", persisted.SqlInstance);
    }

    [Fact]
    public async Task ClearSecretAsync_RemovesTheSecretAndIncrementsVersion()
    {
        var useCase = CreateUseCase(out _, out var secretStore);
        var afterSet = await useCase.UpdateAsync(NewUpdate(InitialVersion, sqlPassword: SqlSentinel));

        var afterClear = await useCase.ClearSecretAsync(AgentSecretKind.SqlPassword, afterSet.Configuration.Version);

        Assert.False(afterClear.HasSqlPassword);
        Assert.Null(await secretStore.TryGetSecretAsync(AgentSecretKind.SqlPassword));
        Assert.True(afterClear.Configuration.Version > afterSet.Configuration.Version);
    }

    [Fact]
    public async Task ClearSecretAsync_WithStaleExpectedVersion_ThrowsAndLeavesSecretIntact()
    {
        var useCase = CreateUseCase(out _, out var secretStore);
        var afterSet = await useCase.UpdateAsync(NewUpdate(InitialVersion, sqlPassword: SqlSentinel));

        await Assert.ThrowsAsync<ConfigurationVersionConflictException>(
            () => useCase.ClearSecretAsync(AgentSecretKind.SqlPassword, afterSet.Configuration.Version - 1));

        Assert.Equal(SqlSentinel, await secretStore.TryGetSecretAsync(AgentSecretKind.SqlPassword));
    }

    private static AgentConfigurationUpdate NewUpdate(long expectedVersion, string? sqlPassword = null, string? rdbPassword = null) => new()
    {
        SqlInstance = string.Empty,
        SqlUser = string.Empty,
        SqlPassword = sqlPassword,
        BranchCode = string.Empty,
        PosNumber = string.Empty,
        ApiBaseUrl = string.Empty,
        Databases = [],
        Services = [],
        Downloader = new AgentDownloaderConfigurationUpdate { RdbPassword = rdbPassword },
        ExpectedVersion = expectedVersion,
    };

    private static AgentConfigurationUseCase CreateUseCase(out FakeAgentConfigurationStore configStore, out FakeAgentSecretStore secretStore)
    {
        configStore = new FakeAgentConfigurationStore();
        secretStore = new FakeAgentSecretStore();
        return new AgentConfigurationUseCase(configStore, secretStore);
    }

    private sealed class FakeAgentConfigurationStore : IAgentConfigurationStore
    {
        private AgentConfiguration _current = new() { Version = 1 };

        public Task<AgentConfiguration> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(_current.Clone());

        public Task SaveAsync(AgentConfiguration configuration, CancellationToken cancellationToken = default)
        {
            _current = configuration.Clone();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAgentSecretStore : IAgentSecretStore
    {
        private readonly Dictionary<AgentSecretKind, string> _secrets = [];

        public Task<bool> HasSecretAsync(AgentSecretKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.ContainsKey(kind));

        public Task<string?> TryGetSecretAsync(AgentSecretKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.TryGetValue(kind, out var value) ? value : null);

        public Task SetSecretAsync(AgentSecretKind kind, string value, CancellationToken cancellationToken = default)
        {
            _secrets[kind] = value;
            return Task.CompletedTask;
        }

        public Task ClearSecretAsync(AgentSecretKind kind, CancellationToken cancellationToken = default)
        {
            _secrets.Remove(kind);
            return Task.CompletedTask;
        }
    }
}
