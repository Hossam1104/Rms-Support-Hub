using System.Collections.Concurrent;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;

internal sealed class InMemoryAgentConfigurationStore : IAgentConfigurationStore
{
    private AgentConfiguration _configuration = new()
    {
        SqlInstance = "127.0.0.1,1",
        SqlUser = "integration-reader",
        BranchCode = "BR-INT",
        PosNumber = "POS-07",
        Release = "2026.08-int07",
        ClientName = "RMS+ Integration",
        ApiBaseUrl = "http://127.0.0.1:1",
        BackupFolder = @"C:\agent-secrets\backups",
        DbFilesPath = @"C:\agent-secrets\db",
        BranchConfigPath = @"C:\agent-secrets\branch.json",
        Databases = ["RmsBranchSrv"],
        Services = ["RMS.BranchService", "RMS.CashierService", "RMSServicesManager"],
        Downloader = new AgentDownloaderConfiguration
        {
            ApiUrl = "https://downloader.integration.test",
            RdbServerIp = "192.0.2.10",
            RdbUsername = "rdb-reader",
            BackupRootFolder = @"\\rdb\backups",
            KnownBranchCodes = ["BR-INT"],
            PollIntervalSeconds = 7,
            TimeoutSeconds = 90
        },
        Version = 7
    };

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
}

internal sealed class InMemoryServiceManager : IServiceManager
{
    private static readonly IReadOnlyDictionary<string, ServiceStatus> Statuses =
        new Dictionary<string, ServiceStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["RMS.BranchService"] = ServiceStatus.Running,
            ["RMS.CashierService"] = ServiceStatus.Stopped,
            ["RMSServicesManager"] = ServiceStatus.Stopped
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
            true,
            RmsDatabaseDiagnosticStatus.Reachable,
            DateTimeOffset.UtcNow,
            "The configured RMS database answered the read-only identity probe."));
    }
}
