using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Runtime;

/// <summary>
/// Projects service-owned Agent configuration and encrypted secrets into the legacy application
/// settings shape required by the already-tested downloader and maintenance services. The
/// projection is process-local; it is never serialized into an Agent response.
/// </summary>
public sealed class AgentRuntimeSettingsFactory(
    IAgentConfigurationStore configurationStore,
    IAgentSecretStore secretStore)
{
    public async Task<AgentRuntimeSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await configurationStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var sqlPassword = await secretStore.TryGetSecretAsync(
            AgentSecretKind.SqlPassword,
            cancellationToken).ConfigureAwait(false);
        var rdbPassword = await secretStore.TryGetSecretAsync(
            AgentSecretKind.RdbPassword,
            cancellationToken).ConfigureAwait(false);

        var downloader = configuration.Downloader ?? new AgentDownloaderConfiguration();
        var maintenance = configuration.Maintenance?.Clone() ?? new MaintenanceSettings();
        var settings = new AppSettings
        {
            SqlInstance = configuration.SqlInstance,
            SqlUser = configuration.SqlUser,
            SqlPassword = sqlPassword ?? string.Empty,
            BranchCode = configuration.BranchCode,
            PosNumber = configuration.PosNumber,
            Release = configuration.Release,
            ClientName = configuration.ClientName,
            ApiBaseUrl = configuration.ApiBaseUrl,
            BackupFolder = configuration.BackupFolder,
            DbFilesPath = configuration.DbFilesPath,
            Databases = configuration.Databases is null ? [] : [.. configuration.Databases],
            Services = configuration.Services is null ? [] : [.. configuration.Services],
            Maintenance = maintenance,
            DbDownloader = new DbDownloaderSettings
            {
                ApiUrl = downloader.ApiUrl,
                RdbServerIp = downloader.RdbServerIp,
                RdbUsername = downloader.RdbUsername,
                RdbPassword = rdbPassword ?? string.Empty,
                BackupRootFolder = downloader.BackupRootFolder,
                KnownBranchCodes = downloader.KnownBranchCodes is null ? [] : [.. downloader.KnownBranchCodes],
                PollIntervalSeconds = downloader.PollIntervalSeconds,
                TimeoutSeconds = downloader.TimeoutSeconds,
                StableSizeObservationAttempts = downloader.StableSizeObservationAttempts,
                StableSizeObservationIntervalSeconds = downloader.StableSizeObservationIntervalSeconds
            }
        };

        return new(configuration, settings);
    }
}

public sealed record AgentRuntimeSettings(
    AgentConfiguration Configuration,
    AppSettings Settings)
{
    public DbDownloaderSettings Downloader => Settings.DbDownloader;

    public MaintenanceSettings Maintenance => Settings.Maintenance;
}
