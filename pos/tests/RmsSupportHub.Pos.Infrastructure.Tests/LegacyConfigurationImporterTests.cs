using RmsSupportHub.Pos.Infrastructure.Configuration;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

/// <summary>
/// Sentinel-only secret values throughout (AGENTS.md safety rules: never test with real
/// production credentials).
/// </summary>
public sealed class LegacyConfigurationImporterTests : IDisposable
{
    private const string SqlPasswordSentinel = "sentinel-legacy-sql-pw";
    private const string RdbPasswordSentinel = "sentinel-legacy-rdb-pw";

    private readonly string _configRootDirectory = Directory.CreateTempSubdirectory("pos-admin-legacy-import-config-").FullName;
    private readonly string _legacyDirectory = Directory.CreateTempSubdirectory("pos-admin-legacy-import-source-").FullName;
    private readonly string _legacyFilePath;

    public LegacyConfigurationImporterTests()
    {
        _legacyFilePath = Path.Combine(_legacyDirectory, "config.json");
    }

    [Fact]
    public async Task ImportAsync_LegacyFileMissing_SucceedsAsANoOp()
    {
        var importer = CreateImporter();

        var result = await importer.ImportAsync();

        Assert.False(result.SourceFound);
        Assert.True(result.Succeeded);
        Assert.Empty(result.FieldsImported);
    }

    [Fact]
    public async Task ImportAsync_ImportsKnownNonSecretFields_AndNeverReadsEitherPassword()
    {
        File.WriteAllText(_legacyFilePath, $$"""
            {
              "sql_instance": "SQLEXPRESS",
              "sql_user": "sa",
              "sql_password": "{{SqlPasswordSentinel}}",
              "branch_code": "P087",
              "pos_number": "1",
              "api_base_url": "https://legacy.example.internal/api",
              "backup_folder": "D:\\Backups",
              "databases": ["POS", "RMS"],
              "services": ["PosService"],
              "db_downloader": {
                "api_url": "https://legacy.example.internal/downloader",
                "rdb_server_ip": "192.0.2.20",
                "rdb_username": "rdb-svc",
                "rdb_password": "{{RdbPasswordSentinel}}",
                "known_branch_codes": ["P001", "P002"],
                "poll_interval_seconds": 10,
                "timeout_seconds": 900
              }
            }
            """);
        var configStore = new JsonAgentConfigurationStore(new AgentConfigurationStoreOptions { RootDirectory = _configRootDirectory });
        var importer = CreateImporter(configStore);

        var result = await importer.ImportAsync();

        Assert.True(result.SourceFound);
        Assert.True(result.Succeeded);
        Assert.Contains("SqlInstance", result.FieldsImported);
        Assert.Contains("Downloader.RdbServerIp", result.FieldsImported);
        Assert.DoesNotContain(result.FieldsImported, f => f.Contains("password", StringComparison.OrdinalIgnoreCase));

        var config = await configStore.LoadAsync();
        Assert.Equal("SQLEXPRESS", config.SqlInstance);
        Assert.Equal("192.0.2.20", config.Downloader.RdbServerIp);
        Assert.Equal(["P001", "P002"], config.Downloader.KnownBranchCodes);
        Assert.Equal(10, config.Downloader.PollIntervalSeconds);

        var secretStore = new DpapiAgentSecretStore(new AgentConfigurationStoreOptions { RootDirectory = _configRootDirectory });
        Assert.False(await secretStore.HasSecretAsync(Domain.Enums.AgentSecretKind.SqlPassword));
        Assert.False(await secretStore.HasSecretAsync(Domain.Enums.AgentSecretKind.RdbPassword));
    }

    [Fact]
    public async Task ImportAsync_PartialData_ImportsOnlyTheFieldsThatArePresent()
    {
        File.WriteAllText(_legacyFilePath, """{ "branch_code": "P099" }""");
        var configStore = new JsonAgentConfigurationStore(new AgentConfigurationStoreOptions { RootDirectory = _configRootDirectory });
        var importer = CreateImporter(configStore);

        var result = await importer.ImportAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(["BranchCode"], result.FieldsImported);
        var config = await configStore.LoadAsync();
        Assert.Equal("P099", config.BranchCode);
        Assert.Equal(string.Empty, config.SqlInstance);
    }

    [Fact]
    public async Task ImportAsync_CorruptJson_ReturnsFailureWithReason()
    {
        File.WriteAllText(_legacyFilePath, "{ not valid json");
        var importer = CreateImporter();

        var result = await importer.ImportAsync();

        Assert.True(result.SourceFound);
        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureReason));
    }

    [Fact]
    public async Task ImportAsync_NonObjectRoot_ReturnsFailureWithReason()
    {
        File.WriteAllText(_legacyFilePath, "[1, 2, 3]");
        var importer = CreateImporter();

        var result = await importer.ImportAsync();

        Assert.True(result.SourceFound);
        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureReason));
    }

    [Fact]
    public async Task ImportAsync_SecondCall_IsIdempotentAndReturnsThePersistedMarkerResult()
    {
        File.WriteAllText(_legacyFilePath, """{ "branch_code": "P099" }""");
        var configStore = new JsonAgentConfigurationStore(new AgentConfigurationStoreOptions { RootDirectory = _configRootDirectory });
        var importer = CreateImporter(configStore);
        var first = await importer.ImportAsync();

        File.WriteAllText(_legacyFilePath, """{ "branch_code": "P111" }""");
        var second = await importer.ImportAsync();

        Assert.Equal(first.ImportedAtUtc, second.ImportedAtUtc);
        Assert.Equal(first.FieldsImported, second.FieldsImported);
        var config = await configStore.LoadAsync();
        Assert.Equal("P099", config.BranchCode);
    }

    [Fact]
    public async Task ImportAsync_NeverModifiesTheLegacyFile()
    {
        const string originalContent = """{ "branch_code": "P099", "sql_password": "sentinel" }""";
        File.WriteAllText(_legacyFilePath, originalContent);
        var originalBytes = await File.ReadAllBytesAsync(_legacyFilePath);
        var importer = CreateImporter();

        await importer.ImportAsync();

        var afterBytes = await File.ReadAllBytesAsync(_legacyFilePath);
        Assert.Equal(originalBytes, afterBytes);
    }

    private LegacyConfigurationImporter CreateImporter(JsonAgentConfigurationStore? configStore = null)
    {
        var storeOptions = new AgentConfigurationStoreOptions { RootDirectory = _configRootDirectory };
        return new LegacyConfigurationImporter(
            new LegacyConfigurationImporterOptions { SourceFilePath = _legacyFilePath },
            storeOptions,
            configStore ?? new JsonAgentConfigurationStore(storeOptions));
    }

    public void Dispose()
    {
        if (Directory.Exists(_configRootDirectory))
        {
            Directory.Delete(_configRootDirectory, recursive: true);
        }

        if (Directory.Exists(_legacyDirectory))
        {
            Directory.Delete(_legacyDirectory, recursive: true);
        }
    }
}
