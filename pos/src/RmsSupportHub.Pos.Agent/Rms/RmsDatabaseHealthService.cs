using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Rms;

/// <summary>
/// Reads bounded backup inventory and capacity evidence from the Agent-owned database storage
/// boundary. It never scans arbitrary directories and never returns a filesystem path.
/// </summary>
public sealed class RmsDatabaseHealthService(
    Artifacts.RmsDatabaseBackupCatalog backupCatalog,
    IBackupFileSystem fileSystem,
    RmsDatabaseStorageOptions storageOptions,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan FreshBackupWindow = TimeSpan.FromDays(7);

    public async Task<RmsDatabaseHealthDto> GetAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken = default)
    {
        var checkedAtUtc = timeProvider.GetUtcNow();
        IReadOnlyList<Artifacts.RmsDatabaseBackupCatalogEntry> backups;
        var inventoryAvailable = true;
        try
        {
            backups = await backupCatalog.ListAsync(database, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            backups = [];
            inventoryAvailable = false;
        }

        var latest = backups.OrderByDescending(entry => entry.CreatedAtUtc).FirstOrDefault();
        var backupAge = latest is null ? (TimeSpan?)null : checkedAtUtc - latest.CreatedAtUtc;
        var backupFreshness = !inventoryAvailable || latest is null || backupAge is null || backupAge.Value < TimeSpan.Zero
            ? FreshnessState.Unknown
            : backupAge <= FreshBackupWindow
                ? FreshnessState.Fresh
                : FreshnessState.Stale;
        var backupState = !inventoryAvailable || backupFreshness == FreshnessState.Unknown
            ? HealthState.Unknown
            : backupFreshness == FreshnessState.Fresh ? HealthState.Healthy : HealthState.Warning;
        var backupSummary = !inventoryAvailable
            ? "Approved backup inventory could not be read."
            : latest is null
            ? "No approved backup is currently available for this database."
            : backupFreshness == FreshnessState.Fresh
                ? "An approved backup is available within the local freshness window."
                : "The newest approved backup is older than the local freshness window.";

        RmsStorageHealthDto storage;
        try
        {
            var destination = fileSystem.InspectDestination(storageOptions.BackupRootPath);
            var rootAvailable = destination.Exists && destination.IsDirectory;
            var state = rootAvailable
                ? destination.AvailableFreeSpaceBytes > 0 ? HealthState.Healthy : HealthState.Warning
                : HealthState.Unknown;
            storage = new(
                state,
                rootAvailable ? Math.Max(0, destination.AvailableFreeSpaceBytes) : null,
                rootAvailable,
                FreshnessState.Fresh,
                rootAvailable
                    ? "The approved backup storage root is available for local evidence."
                    : "The approved backup storage root is not currently available.");
        }
        catch
        {
            storage = new(
                HealthState.Unknown,
                null,
                false,
                FreshnessState.Unknown,
                "Storage capacity evidence is unavailable.");
        }

        return new(
            new(backups.Count, latest?.CreatedAtUtc, backupFreshness, backupState, backupSummary),
            storage);
    }
}
