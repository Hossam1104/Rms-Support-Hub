using RmsSupportHub.Pos.Infrastructure.Configuration;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

public sealed class JsonAgentConfigurationStoreTests : IDisposable
{
    private readonly string _rootDirectory = Directory.CreateTempSubdirectory("pos-admin-config-store-tests-").FullName;

    [Fact]
    public async Task LoadAsync_OnAFreshDirectory_ReturnsDefaultsWithNoCredentialOrAddress()
    {
        var store = new JsonAgentConfigurationStore(new AgentConfigurationStoreOptions { RootDirectory = _rootDirectory });

        var config = await store.LoadAsync();

        Assert.Equal(1, config.Version);
        Assert.Equal(string.Empty, config.SqlInstance);
        Assert.Equal(string.Empty, config.ApiBaseUrl);
        Assert.Equal(string.Empty, config.Downloader.ApiUrl);
        Assert.Equal(string.Empty, config.Downloader.RdbServerIp);
        Assert.Empty(config.Databases);
        Assert.Empty(config.Downloader.KnownBranchCodes);
        Assert.True(File.Exists(Path.Combine(_rootDirectory, "configuration.json")));
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsAllNonSecretFields()
    {
        var store = new JsonAgentConfigurationStore(new AgentConfigurationStoreOptions { RootDirectory = _rootDirectory });
        var config = await store.LoadAsync();
        config.SqlInstance = "SQLEXPRESS";
        config.BranchCode = "P087";
        config.Databases = ["POS", "RMS"];
        config.Downloader.RdbServerIp = "192.0.2.10";
        config.Downloader.KnownBranchCodes = ["P001", "P002"];
        config.Version = 2;

        await store.SaveAsync(config);
        var reloaded = await store.LoadAsync();

        Assert.Equal("SQLEXPRESS", reloaded.SqlInstance);
        Assert.Equal("P087", reloaded.BranchCode);
        Assert.Equal(["POS", "RMS"], reloaded.Databases);
        Assert.Equal("192.0.2.10", reloaded.Downloader.RdbServerIp);
        Assert.Equal(["P001", "P002"], reloaded.Downloader.KnownBranchCodes);
        Assert.Equal(2, reloaded.Version);
    }

    [Fact]
    public async Task LoadAsync_ReturnsAClone_MutatingItDoesNotAffectWhatIsPersisted()
    {
        var store = new JsonAgentConfigurationStore(new AgentConfigurationStoreOptions { RootDirectory = _rootDirectory });
        var first = await store.LoadAsync();
        first.SqlInstance = "MUTATED-IN-MEMORY-ONLY";

        var second = await store.LoadAsync();

        Assert.Equal(string.Empty, second.SqlInstance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
