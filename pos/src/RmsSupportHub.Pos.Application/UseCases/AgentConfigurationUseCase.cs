using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Exceptions;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.UseCases;

/// <summary>
/// Owns the redaction, keep/replace/clear, and optimistic-concurrency policy for the Agent's
/// service-owned configuration (plan section 5.5). A blank or omitted secret field on
/// <see cref="AgentConfigurationUpdate"/> always means "keep the current secret" — clearing only
/// ever happens via <see cref="ClearSecretAsync"/>. All mutations serialize through one lock so a
/// concurrent config update and secret clear cannot interleave and desynchronize the version token.
/// </summary>
public sealed class AgentConfigurationUseCase(
    IAgentConfigurationStore configurationStore,
    IAgentSecretStore secretStore)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<AgentConfigurationSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var config = await configurationStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return await SnapshotAsync(config, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AgentConfigurationSnapshot> UpdateAsync(AgentConfigurationUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await configurationStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (update.ExpectedVersion != current.Version)
            {
                throw new ConfigurationVersionConflictException(update.ExpectedVersion, current.Version);
            }

            current.SqlInstance = update.SqlInstance;
            current.SqlUser = update.SqlUser;
            current.BranchCode = update.BranchCode;
            current.PosNumber = update.PosNumber;
            current.Release = update.Release;
            current.ClientName = update.ClientName;
            current.ApiBaseUrl = update.ApiBaseUrl;
            current.Databases = [.. update.Databases];
            current.Services = [.. update.Services];
            current.Downloader.ApiUrl = update.Downloader.ApiUrl;
            current.Downloader.RdbServerIp = update.Downloader.RdbServerIp;
            current.Downloader.RdbUsername = update.Downloader.RdbUsername;
            current.Downloader.KnownBranchCodes = [.. update.Downloader.KnownBranchCodes];
            current.Downloader.PollIntervalSeconds = update.Downloader.PollIntervalSeconds;
            current.Downloader.TimeoutSeconds = update.Downloader.TimeoutSeconds;
            current.Version += 1;

            await configurationStore.SaveAsync(current, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(update.SqlPassword))
            {
                await secretStore.SetSecretAsync(AgentSecretKind.SqlPassword, update.SqlPassword, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(update.Downloader.RdbPassword))
            {
                await secretStore.SetSecretAsync(AgentSecretKind.RdbPassword, update.Downloader.RdbPassword, cancellationToken).ConfigureAwait(false);
            }

            return await SnapshotAsync(current, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AgentConfigurationSnapshot> ClearSecretAsync(AgentSecretKind kind, long expectedVersion, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await configurationStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (expectedVersion != current.Version)
            {
                throw new ConfigurationVersionConflictException(expectedVersion, current.Version);
            }

            await secretStore.ClearSecretAsync(kind, cancellationToken).ConfigureAwait(false);

            current.Version += 1;
            await configurationStore.SaveAsync(current, cancellationToken).ConfigureAwait(false);

            return await SnapshotAsync(current, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<AgentConfigurationSnapshot> SnapshotAsync(AgentConfiguration config, CancellationToken cancellationToken)
    {
        var hasSql = await secretStore.HasSecretAsync(AgentSecretKind.SqlPassword, cancellationToken).ConfigureAwait(false);
        var hasRdb = await secretStore.HasSecretAsync(AgentSecretKind.RdbPassword, cancellationToken).ConfigureAwait(false);
        return new AgentConfigurationSnapshot(config, hasSql, hasRdb);
    }
}
