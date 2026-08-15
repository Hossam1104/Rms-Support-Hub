using System.Collections.Concurrent;
using System.Text;
using RmsSupportHub.Pos.Application.Maintenance;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;

internal sealed class InMemoryAgentConfigurationStore : IAgentConfigurationStore
{
    private AgentConfiguration _configuration;

    public InMemoryAgentConfigurationStore(string storageRoot)
    {
        var maintenanceRoot = Path.Combine(storageRoot, "maintenance");
        _configuration = new AgentConfiguration
        {
            SqlInstance = "127.0.0.1,1",
            SqlUser = "integration-reader",
            BranchCode = "BR-INT",
            PosNumber = "POS-07",
            Release = "2026.08-int07",
            ClientName = "RMS+ Integration",
            ApiBaseUrl = "http://127.0.0.1:1",
            BackupFolder = Path.Combine(storageRoot, "backups"),
            DbFilesPath = Path.Combine(storageRoot, "db"),
            BranchConfigPath = Path.Combine(storageRoot, "branch.json"),
            Databases = ["RmsBranchSrv"],
            Services = ["RMS.BranchService", "RMS.CashierService", "RMSServiceManager"],
            Downloader = new AgentDownloaderConfiguration
            {
                ApiUrl = "https://downloader.integration.test",
                RdbServerIp = "192.0.2.10",
                RdbUsername = "rdb-reader",
                BackupRootFolder = @"\\rdb\backups",
                KnownBranchCodes = ["BR-INT"],
                PollIntervalSeconds = 1,
                TimeoutSeconds = 30,
                StableSizeObservationAttempts = 2,
                StableSizeObservationIntervalSeconds = 1
            },
            Maintenance = new MaintenanceSettings
            {
                CleanupTargets = [Path.Combine(maintenanceRoot, "cache")],
                ManagedRoots = [maintenanceRoot],
                DataRoots = [maintenanceRoot],
                InstallRoots = [Path.Combine(storageRoot, "install")],
                ProtectedRoots = [Path.Combine(storageRoot, "protected")]
            },
            Version = 7
        };
    }

    public Task<AgentConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_configuration.Clone());
    }

    public Task SaveAsync(AgentConfiguration configuration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _configuration = configuration.Clone();
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryAgentSecretStore : IAgentSecretStore
{
    private readonly Dictionary<AgentSecretKind, string> _secrets = new();

    public Task<bool> HasSecretAsync(AgentSecretKind kind, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_secrets.ContainsKey(kind));
    }

    public Task<string?> TryGetSecretAsync(AgentSecretKind kind, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _secrets.TryGetValue(kind, out var value);
        return Task.FromResult(value);
    }

    public Task SetSecretAsync(AgentSecretKind kind, string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _secrets[kind] = value;
        return Task.CompletedTask;
    }

    public Task ClearSecretAsync(AgentSecretKind kind, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _secrets.Remove(kind);
        return Task.CompletedTask;
    }

    public void EnableDownloaderCredential() => _secrets[AgentSecretKind.RdbPassword] = Guid.NewGuid().ToString("N");
}

internal sealed class InMemoryBackupApiClient : IBackupApiClient
{
    public DownloaderTriggerResult Result { get; set; } = new(DownloaderTriggerState.Accepted);

    public Func<string, IReadOnlyList<string>, CancellationToken, Task<DownloaderTriggerResult>>? TriggerBehavior { get; set; }

    public Task<DownloaderTriggerResult> TriggerBackupAsync(
        string apiUrl,
        IReadOnlyList<string> branchCodes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (TriggerBehavior is not null)
        {
            return TriggerBehavior(apiUrl, branchCodes, cancellationToken);
        }

        return Task.FromResult(Result);
    }
}

internal sealed class InMemoryBackupRepository : IBackupRepository
{
    private const string BatchName = "batch-001";
    private const string ArchiveName = "BR-INT_0042.zip";

    public Task<IReadOnlyList<RemoteEntryInfo>> ListDirectoriesAsync(
        RemoteConnectionInfo connection,
        string rootFolder,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<RemoteEntryInfo>>([
            new(BatchName, $"{rootFolder}\\{BatchName}", DateTimeOffset.UtcNow, 0)
        ]);
    }

    public Task<IReadOnlyList<RemoteEntryInfo>> ListFilesAsync(
        RemoteConnectionInfo connection,
        string folder,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<RemoteEntryInfo>>([
            new(ArchiveName, $"{folder}\\{ArchiveName}", DateTimeOffset.UtcNow, 32)
        ]);
    }

    public async Task DownloadFileAsync(
        RemoteConnectionInfo connection,
        string remoteFilePath,
        string localFilePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(localFilePath, [0x50, 0x4B, 0x03, 0x04, 0x49, 0x4E, 0x54], cancellationToken);
        progress?.Report(100);
    }
}

internal sealed class ImmediateDownloaderDelay : IDownloaderDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryMaintenanceFileSystem : IMaintenanceFileSystem
{
    public ConcurrentQueue<(string Path, bool Recursive)> DeleteCalls { get; } = new();

    public string ExpandEnvironmentVariables(string path) => Environment.ExpandEnvironmentVariables(path);

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public MaintenancePathInspection Inspect(string path) =>
        new(path, true, true, false, null, null, 0);

    public IReadOnlyList<MaintenancePathInspection> InspectAncestors(string path) =>
        [new(path, true, true, false, null, null, 0)];

    public long? TryGetAvailableFreeSpace(string path) => 1024L * 1024L * 1024L;

    public Task DeleteAsync(string path, bool recursive, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteCalls.Enqueue((path, recursive));
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryMaintenanceDatabase : IDatabaseService, IMaintenanceDatabasePreview, IMaintenanceDatabaseReset
{
    public ConcurrentQueue<(string DatabaseName, string BranchCode, IReadOnlyList<string> Tables)> ResetCalls { get; } = new();

    public Task TestConnectionAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<bool> BranchExistsAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }

    public Task ResetBranchDataAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task BackupDatabaseAsync(AppSettings settings, string databaseName, string backupFilePath, bool useCompatibilityMode, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<RestoreFileInfo>> ReadRestoreFileListAsync(AppSettings settings, string backupFilePath, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RestoreFileInfo>>([]);

    public Task RestoreDatabaseAsync(AppSettings settings, string targetDatabase, string backupFilePath, IReadOnlyList<RestoreFileInfo> logicalFiles, string dbFilesPath, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> BranchExistsInDatabaseAsync(AppSettings settings, string databaseName, string branchCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<MaintenanceTableScope>> GetBranchResetScopeAsync(
        AppSettings settings,
        string databaseName,
        string branchCode,
        IReadOnlyList<string> tableNames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<MaintenanceTableScope>>(
            tableNames.Select(table => new MaintenanceTableScope(table, 3)).ToArray());
    }

    public Task ResetBranchDataAsync(
        AppSettings settings,
        string databaseName,
        string branchCode,
        IReadOnlyList<string> tableNames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ResetCalls.Enqueue((databaseName, branchCode, tableNames));
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class InMemoryServiceManager : IServiceManager
{
    private static readonly IReadOnlyDictionary<string, ServiceStatus> Statuses =
        new Dictionary<string, ServiceStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["RMS.BranchService"] = ServiceStatus.Running,
            ["RMS.CashierService"] = ServiceStatus.Stopped,
            ["RMSServiceManager"] = ServiceStatus.Stopped
        };

    public ConcurrentQueue<(string ServiceName, ServiceControlAction Action)> ControlCalls { get; } = new();

    public Func<string, ServiceControlAction, CancellationToken, Task>? ControlBehavior { get; set; }

    public Task<ServiceStatus> GetStatusAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Statuses.GetValueOrDefault(serviceName, ServiceStatus.Unknown));
    }

    public Task<IReadOnlyDictionary<string, ServiceStatus>> GetStatusesAsync(
        IEnumerable<string> serviceNames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = serviceNames.ToDictionary(
            name => name,
            name => Statuses.GetValueOrDefault(name, ServiceStatus.Unknown),
            StringComparer.OrdinalIgnoreCase);
        return Task.FromResult<IReadOnlyDictionary<string, ServiceStatus>>(result);
    }

    public Task ControlAsync(
        string serviceName,
        ServiceControlAction action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ControlCalls.Enqueue((serviceName, action));
        return ControlBehavior?.Invoke(serviceName, action, cancellationToken) ?? Task.CompletedTask;
    }
}

internal sealed class InMemoryRmsDiagnosticEvidenceReader : IRmsDiagnosticEvidenceReader
{
    public Task<RmsDiagnosticEvidenceReadResult> ReadAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RmsDiagnosticEvidenceReadResult([], []));
    }
}

internal sealed class InMemoryRmsInstallationDiscovery : IRmsInstallationDiscovery
{
    public Task<RmsInstallationSnapshot> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RmsInstallationSnapshot(
            true,
            true,
            true,
            "BR-INT",
            "POS-07",
            "1",
            "1",
            "integration-installation-guid",
            "https://main.integration.test:8443",
            "localhost",
            "RMS+ Integration",
            "2026.08-int07",
            new("2026.08", "2026.08", "2026.08"),
            new(
                RmsConsistencyState.Consistent,
                RmsConsistencyState.Consistent,
                RmsConsistencyState.Consistent,
                RmsConsistencyState.Consistent,
                RmsConsistencyState.Consistent,
                []),
            new(true, RmsConnectionStringState.Valid, "127.0.0.1,1", "RmsBranchSrv", false),
            new(true, RmsConnectionStringState.Valid, "127.0.0.1,1", "RmsCashierSrv", false),
            new(RmsEndpointConfigurationState.Unavailable, null, null, null),
            new(RmsEndpointConfigurationState.Unavailable, null, null, null),
            new(true, true, true, true)));
    }
}

internal sealed class InMemoryRmsDatabaseDiagnostics : IRmsDatabaseDiagnostics
{
    public RmsDatabaseDiagnosticStatus Status { get; set; } = RmsDatabaseDiagnosticStatus.Reachable;

    public bool? DatabaseNameMatches { get; set; } = true;

    /// <summary>Status returned by the master/server-only probe used to preflight Restore.</summary>
    public RmsDatabaseDiagnosticStatus ServerStatus { get; set; } = RmsDatabaseDiagnosticStatus.Reachable;

    public bool? ServerDatabaseNameMatches { get; set; } = true;

    public Task<RmsDatabaseDiagnosticResult> DiagnoseAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expected = database == RmsDatabaseKind.Branch ? "RmsBranchSrv" : "RmsCashierSrv";
        return Task.FromResult(new RmsDatabaseDiagnosticResult(
            database,
            expected,
            expected,
            "integration-sql:1433",
            true,
            DatabaseNameMatches,
            Status,
            DateTimeOffset.UtcNow,
            "The configured RMS database answered the read-only identity probe."));
    }

    public Task<RmsDatabaseDiagnosticResult> DiagnoseServerAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expected = database == RmsDatabaseKind.Branch ? "RmsBranchSrv" : "RmsCashierSrv";
        return Task.FromResult(new RmsDatabaseDiagnosticResult(
            database,
            expected,
            expected,
            "integration-sql:1433",
            true,
            ServerDatabaseNameMatches,
            ServerStatus,
            DateTimeOffset.UtcNow,
            "The SQL Server master connection answered the read-only connectivity probe."));
    }
}

internal sealed class InMemoryRmsDatabaseSqlOperations : IRmsDatabaseSqlOperations
{
    public ConcurrentQueue<(RmsDatabaseKind Database, string BackupPath)> BackupCalls { get; } = new();

    public ConcurrentQueue<(RmsDatabaseKind Database, string BackupPath)> RestoreCalls { get; } = new();

    public Func<RmsDatabaseKind, CancellationToken, Task>? BackupBehavior { get; set; }

    public Func<RmsDatabaseKind, CancellationToken, Task>? RestoreBehavior { get; set; }

    public RmsDatabaseSqlOutcome BackupOutcome { get; set; } = RmsDatabaseSqlOutcome.Completed;

    public RmsDatabaseSqlOutcome RestoreOutcome { get; set; } = RmsDatabaseSqlOutcome.Completed;

    public bool VerifyResult { get; set; } = true;

    public async Task<RmsDatabaseSqlBackupResult> BackupAsync(
        RmsDatabaseKind database,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        BackupCalls.Enqueue((database, backupPath));
        if (BackupBehavior is not null)
        {
            await BackupBehavior(database, cancellationToken);
        }

        if (BackupOutcome != RmsDatabaseSqlOutcome.Completed)
        {
            return BackupOutcome switch
            {
                RmsDatabaseSqlOutcome.OutcomeUnknown => new(
                    BackupOutcome,
                    "backup_sql_outcome_unknown",
                    "The synthetic SQL backup dispatch outcome is unknown."),
                _ => new(
                    BackupOutcome,
                    "backup_sql_failed",
                    "The synthetic SQL backup was rejected before completion.")
            };
        }

        var directory = Path.GetDirectoryName(backupPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            backupPath,
            $"synthetic-{database}-{Guid.NewGuid():N}",
            Encoding.UTF8,
            cancellationToken);
        return new(
            RmsDatabaseSqlOutcome.Completed,
            "backup_sql_completed",
            "The synthetic SQL backup completed.");
    }

    public Task<RmsDatabaseSqlInspectionResult> InspectBackupAsync(
        RmsDatabaseKind database,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            new RmsDatabaseSqlInspectionResult(
                RmsDatabaseSqlOutcome.Completed,
                "restore_sql_inspection_completed",
                "The synthetic backup passed inspection.",
                [new RestoreFileInfo($"{database}-data", "D"), new RestoreFileInfo($"{database}-log", "L")]));
    }

    public async Task<RmsDatabaseSqlRestoreResult> RestoreAsync(
        RmsDatabaseKind database,
        string backupPath,
        IReadOnlyList<RestoreFileInfo> logicalFiles,
        string databaseFilesRoot,
        CancellationToken cancellationToken = default)
    {
        RestoreCalls.Enqueue((database, backupPath));
        if (RestoreBehavior is not null)
        {
            await RestoreBehavior(database, cancellationToken);
        }

        return RestoreOutcome switch
        {
            RmsDatabaseSqlOutcome.Completed => new(
                RestoreOutcome,
                "restore_sql_completed",
                "The synthetic SQL restore completed.",
                false,
                []),
            RmsDatabaseSqlOutcome.OutcomeUnknown => new(
                RestoreOutcome,
                "restore_sql_outcome_unknown",
                "The synthetic SQL restore dispatch outcome is unknown.",
                true,
                ["Synthetic restore recovery was attempted."]),
            _ => new(
                RestoreOutcome,
                "restore_sql_failed",
                "The synthetic SQL restore was rejected.",
                false,
                [])
        };
    }

    public Task<bool> VerifyDatabaseAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(VerifyResult);
    }
}
