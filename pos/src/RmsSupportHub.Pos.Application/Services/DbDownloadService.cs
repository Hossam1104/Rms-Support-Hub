using System.Text.RegularExpressions;
using RmsSupportHub.Pos.Application.Pathing;
using RmsSupportHub.Pos.Domain.Exceptions;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Services;

/// <summary>
/// Reusable downloader orchestration. It deliberately keeps remote/local paths in the internal
/// adapter model only; Agent callers project branch-safe state and opaque artifact capabilities.
/// </summary>
public sealed partial class DbDownloadService
{
    private readonly IBackupApiClient _apiClient;
    private readonly IBackupRepository _backupRepository;
    private readonly TimeProvider _timeProvider;
    private readonly IDownloaderDelay _delay;

    public DbDownloadService(
        IBackupApiClient apiClient,
        IBackupRepository backupRepository,
        TimeProvider? timeProvider = null,
        IDownloaderDelay? delay = null)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _backupRepository = backupRepository ?? throw new ArgumentNullException(nameof(backupRepository));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _delay = delay ?? new SystemDownloaderDelay();
    }

    public async Task<BackupJob> RunAsync(
        DbDownloaderSettings settings,
        IReadOnlyList<string> branchCodes,
        Action<BranchBackupItem>? onItemChanged = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return (await RunWithOutcomeAsync(
            settings,
            branchCodes,
            onItemChanged,
            progress,
            cancellationToken).ConfigureAwait(false)).Job;
    }

    /// <summary>
    /// Runs trigger and discovery while preserving the accepted-trigger milestone across every
    /// later repository, download, and cancellation outcome. The compatibility <see cref="RunAsync"/>
    /// method returns only the job; Agent execution uses this richer result.
    /// </summary>
    public async Task<DownloaderExecutionResult> RunWithOutcomeAsync(
        DbDownloaderSettings settings,
        IReadOnlyList<string> branchCodes,
        Action<BranchBackupItem>? onItemChanged = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        DownloaderInputPolicy.ValidateSettings(settings);
        var normalizedBranches = DownloaderInputPolicy.NormalizeBranchCodes(branchCodes);
        var job = new BackupJob(normalizedBranches, _timeProvider);
        var connection = new RemoteConnectionInfo(
            settings.RdbServerIp,
            settings.RdbUsername,
            settings.RdbPassword,
            settings.BackupRootFolder);
        var deadline = job.TriggeredAtUtc.AddSeconds(settings.TimeoutSeconds);

        try
        {
            progress?.Report($"Triggering backup job for {job.Items.Count} branch(es)...");
            var trigger = await _apiClient.TriggerBackupAsync(
                settings.ApiUrl,
                normalizedBranches,
                cancellationToken).ConfigureAwait(false);

            if (trigger.State != DownloaderTriggerState.Accepted)
            {
                if (trigger.State == DownloaderTriggerState.NotAttempted
                    && cancellationToken.IsCancellationRequested
                    && trigger.FailureCode is null)
                {
                    MarkCancelled(job.Items, onItemChanged);
                }
                else
                {
                    var failureCode = trigger.FailureCode
                        ?? (trigger.State == DownloaderTriggerState.OutcomeUnknown
                            ? DownloaderFailureCodes.TriggerOutcomeUnknown
                            : DownloaderFailureCodes.TriggerFailed);
                    var message = trigger.State == DownloaderTriggerState.OutcomeUnknown
                        ? DownloaderOperatorGuidance.TriggerOutcomeUnknown
                        : "The backup trigger was rejected.";
                    MarkFailed(job.Items, failureCode, message, onItemChanged);
                    return new DownloaderExecutionResult(job, trigger.State, failureCode);
                }

                return new DownloaderExecutionResult(job, trigger.State);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The typed trigger result is responsible for proving pre-dispatch cancellation. A
            // bare cancellation escaping an implementation cannot prove that the POST was never
            // dispatched, so preserve truthful uncertainty at this boundary.
            MarkFailed(
                job.Items,
                DownloaderFailureCodes.TriggerOutcomeUnknown,
                DownloaderOperatorGuidance.TriggerOutcomeUnknown,
                onItemChanged);
            return new DownloaderExecutionResult(
                job,
                DownloaderTriggerState.OutcomeUnknown,
                DownloaderFailureCodes.TriggerOutcomeUnknown);
        }
        catch (DownloaderTriggerException exception)
        {
            var failureCode = exception.TriggerState == DownloaderTriggerState.OutcomeUnknown
                ? DownloaderFailureCodes.TriggerOutcomeUnknown
                : exception.Code;
            var message = exception.TriggerState == DownloaderTriggerState.OutcomeUnknown
                ? DownloaderOperatorGuidance.TriggerOutcomeUnknown
                : "The backup trigger was rejected.";
            MarkFailed(job.Items, failureCode, message, onItemChanged);
            return new DownloaderExecutionResult(job, exception.TriggerState, failureCode);
        }
        catch
        {
            MarkFailed(
                job.Items,
                DownloaderFailureCodes.TriggerOutcomeUnknown,
                DownloaderOperatorGuidance.TriggerOutcomeUnknown,
                onItemChanged);
            return new DownloaderExecutionResult(
                job,
                DownloaderTriggerState.OutcomeUnknown,
                DownloaderFailureCodes.TriggerOutcomeUnknown);
        }

        foreach (var item in job.Items)
        {
            item.Status = BranchBackupStatus.Triggered;
            Notify(item, onItemChanged);
        }

        try
        {
            var batchFolder = await DiscoverBatchFolderAsync(
                connection,
                settings.BackupRootFolder,
                job.TriggeredAtUtc,
                deadline,
                progress,
                cancellationToken).ConfigureAwait(false);

            if (batchFolder is null)
            {
                foreach (var item in job.Items.Where(item => !IsTerminal(item.Status)))
                {
                    item.Status = BranchBackupStatus.TimedOut;
                    item.FailureCode = DownloaderFailureCodes.BatchFolderTimeout;
                    item.ErrorMessage = "The backup batch did not appear before the timeout.";
                    Notify(item, onItemChanged);
                }

                progress?.Report("The backup batch did not appear before the timeout.");
                return new DownloaderExecutionResult(job, DownloaderTriggerState.Accepted);
            }

            job.BatchFolderPath = batchFolder.FullPath;
            progress?.Report("A backup batch was detected.");
            foreach (var item in job.Items.Where(item => item.Status == BranchBackupStatus.Triggered))
            {
                item.Status = BranchBackupStatus.Waiting;
                Notify(item, onItemChanged);
            }

            var pending = job.Items
                .Where(item => item.Status is BranchBackupStatus.Waiting or BranchBackupStatus.Pending)
                .ToList();
            while (pending.Count > 0 && _timeProvider.GetUtcNow() < deadline && !cancellationToken.IsCancellationRequested)
            {
                var files = await _backupRepository.ListFilesAsync(connection, batchFolder.FullPath, cancellationToken).ConfigureAwait(false);

                foreach (var item in pending.ToList())
                {
                    var match = files
                        .Where(file => IsZipForBranch(file.Name, item.BranchCode))
                        .OrderByDescending(file => file.CreatedAtUtc)
                        .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault();
                    if (match is null)
                    {
                        continue;
                    }

                    job.Serial ??= ExtractSerial(match.Name);
                    item.RemoteZipPath = match.FullPath;
                    item.Status = BranchBackupStatus.ZipDetected;
                    item.FailureCode = null;
                    item.ErrorMessage = null;
                    Notify(item, onItemChanged);
                    progress?.Report($"{item.BranchCode}: backup archive detected.");

                    item.Status = BranchBackupStatus.Validating;
                    Notify(item, onItemChanged);
                    var stableSize = await IsFileStableAsync(
                        connection,
                        match,
                        settings,
                        deadline,
                        cancellationToken).ConfigureAwait(false);

                    if (stableSize is null)
                    {
                        item.Status = BranchBackupStatus.Waiting;
                        item.RemoteZipPath = null;
                        item.FailureCode = _timeProvider.GetUtcNow() >= deadline
                            ? DownloaderFailureCodes.StableSizeTimeout
                            : null;
                        item.ErrorMessage = null;
                        Notify(item, onItemChanged);
                        continue;
                    }

                    item.Status = BranchBackupStatus.Ready;
                    item.LastObservedSizeBytes = stableSize.Value;
                    item.FailureCode = null;
                    item.ErrorMessage = null;
                    Notify(item, onItemChanged);
                    progress?.Report($"{item.BranchCode}: backup archive is ready.");
                    pending.Remove(item);
                }

                if (pending.Count > 0)
                {
                    await DelayBeforeDeadlineAsync(
                        TimeSpan.FromSeconds(settings.PollIntervalSeconds),
                        deadline,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                MarkCancelled(job.Items, onItemChanged);
                return new DownloaderExecutionResult(job, DownloaderTriggerState.Accepted);
            }

            foreach (var item in pending)
            {
                item.Status = BranchBackupStatus.TimedOut;
                item.FailureCode = item.FailureCode == DownloaderFailureCodes.StableSizeTimeout
                    ? DownloaderFailureCodes.StableSizeTimeout
                    : DownloaderFailureCodes.ZipTimeout;
                item.ErrorMessage = "The branch archive did not become ready before the timeout.";
                Notify(item, onItemChanged);
                progress?.Report($"{item.BranchCode}: timed out waiting for a ready archive.");
            }

            return new DownloaderExecutionResult(job, DownloaderTriggerState.Accepted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MarkCancelled(job.Items, onItemChanged);
            return new DownloaderExecutionResult(job, DownloaderTriggerState.Accepted);
        }
        catch (BackupRepositoryException exception)
        {
            MarkFailed(
                job.Items,
                exception.Code,
                "The backup repository could not be accessed.",
                onItemChanged,
                preserveReady: true);
            return new DownloaderExecutionResult(job, DownloaderTriggerState.Accepted, exception.Code);
        }
        catch
        {
            MarkFailed(
                job.Items,
                DownloaderFailureCodes.SmbRepositoryFailed,
                "The backup repository could not be accessed.",
                onItemChanged,
                preserveReady: true);
            return new DownloaderExecutionResult(job, DownloaderTriggerState.Accepted, DownloaderFailureCodes.SmbRepositoryFailed);
        }
    }

    public async Task DownloadAsync(
        DbDownloaderSettings settings,
        BranchBackupItem item,
        string localFolder,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        Action<BranchBackupItem>? onItemChanged = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        DownloaderInputPolicy.ValidateSettings(settings);
        _ = DownloaderInputPolicy.NormalizeBranchCodes([item.BranchCode]);

        var remoteZipPath = item.RemoteZipPath;
        var fileName = WindowsPathSemantics.GetFileName(remoteZipPath);
        if (remoteZipPath is null
            || fileName is null
            || !DownloaderInputPolicy.IsSafeArchiveFileName(fileName))
        {
            item.Status = BranchBackupStatus.Failed;
            item.FailureCode = DownloaderFailureCodes.InvalidConfiguration;
            item.ErrorMessage = "The branch archive is not ready for download.";
            Notify(item, onItemChanged);
            throw new InvalidOperationException("The branch archive is not ready for download.");
        }

        Directory.CreateDirectory(localFolder);
        var localPath = Path.Combine(localFolder, fileName);
        var connection = new RemoteConnectionInfo(
            settings.RdbServerIp,
            settings.RdbUsername,
            settings.RdbPassword,
            settings.BackupRootFolder);

        item.Status = BranchBackupStatus.Downloading;
        item.FailureCode = null;
        item.ErrorMessage = null;
        Notify(item, onItemChanged);
        try
        {
            await _backupRepository.DownloadFileAsync(
                connection,
                remoteZipPath,
                localPath,
                progress,
                cancellationToken).ConfigureAwait(false);
            item.LocalDownloadPath = localPath;
            item.Status = BranchBackupStatus.Downloaded;
            item.FailureCode = null;
            item.ErrorMessage = null;
            Notify(item, onItemChanged);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            item.Status = BranchBackupStatus.Cancelled;
            item.FailureCode = DownloaderFailureCodes.DownloadCancelled;
            item.ErrorMessage = "The branch download was cancelled.";
            Notify(item, onItemChanged);
            throw;
        }
        catch (BackupRepositoryException exception)
        {
            item.Status = BranchBackupStatus.Failed;
            item.FailureCode = exception.Code;
            item.ErrorMessage = "The backup repository could not be accessed.";
            Notify(item, onItemChanged);
            throw;
        }
        catch
        {
            item.Status = BranchBackupStatus.Failed;
            item.FailureCode = DownloaderFailureCodes.DownloadFailed;
            item.ErrorMessage = "The branch archive could not be downloaded.";
            Notify(item, onItemChanged);
            throw new InvalidOperationException("The branch archive could not be downloaded.");
        }
    }

    private async Task<RemoteEntryInfo?> DiscoverBatchFolderAsync(
        RemoteConnectionInfo connection,
        string rootFolder,
        DateTimeOffset triggeredAtUtc,
        DateTimeOffset deadline,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        while (_timeProvider.GetUtcNow() < deadline && !cancellationToken.IsCancellationRequested)
        {
            var directories = await _backupRepository.ListDirectoriesAsync(connection, rootFolder, cancellationToken).ConfigureAwait(false);
            var candidates = directories
                .Where(directory => directory.CreatedAtUtc >= triggeredAtUtc.AddSeconds(-5))
                .OrderByDescending(directory => directory.CreatedAtUtc)
                .ThenBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (candidates.Count > 1)
            {
                progress?.Report($"{candidates.Count} new backup batches were found; using the newest.");
            }

            if (candidates.Count > 0)
            {
                return candidates[0];
            }

            await DelayBeforeDeadlineAsync(TimeSpan.FromSeconds(2), deadline, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<long?> IsFileStableAsync(
        RemoteConnectionInfo connection,
        RemoteEntryInfo file,
        DbDownloaderSettings settings,
        DateTimeOffset deadline,
        CancellationToken cancellationToken)
    {
        // Once a concrete archive has been detected, finish the bounded stability proof even if
        // the outer discovery deadline has elapsed. This preserves the legacy behavior where an
        // already-present archive can become Ready while other branches still time out; the
        // stability window itself is bounded by attempts and remains cancellation-aware.
        var lastSize = file.SizeBytes;
        var stableObservations = 0;
        for (var attempt = 0; attempt < settings.StableSizeObservationAttempts; attempt++)
        {
            await _delay.DelayAsync(
                TimeSpan.FromSeconds(settings.StableSizeObservationIntervalSeconds),
                cancellationToken).ConfigureAwait(false);
            var parent = WindowsPathSemantics.GetDirectoryName(file.FullPath) ?? string.Empty;
            var refreshed = await _backupRepository.ListFilesAsync(connection, parent, cancellationToken).ConfigureAwait(false);
            var current = refreshed.FirstOrDefault(candidate =>
                string.Equals(candidate.FullPath, file.FullPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Name, file.Name, StringComparison.OrdinalIgnoreCase));
            if (current is null || current.SizeBytes <= 0)
            {
                stableObservations = 0;
                continue;
            }

            if (current.SizeBytes == lastSize && lastSize > 0)
            {
                stableObservations++;
                if (stableObservations >= 1) return current.SizeBytes;
            }
            else
            {
                lastSize = current.SizeBytes;
                stableObservations = 0;
            }
        }

        return null;
    }

    private async Task DelayBeforeDeadlineAsync(TimeSpan requested, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        var remaining = deadline - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero) return;
        await _delay.DelayAsync(requested <= remaining ? requested : remaining, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsTerminal(BranchBackupStatus status) => status is
        BranchBackupStatus.Downloaded
        or BranchBackupStatus.Failed
        or BranchBackupStatus.TimedOut
        or BranchBackupStatus.Cancelled;

    private static void MarkCancelled(
        IEnumerable<BranchBackupItem> items,
        Action<BranchBackupItem>? onItemChanged)
    {
        foreach (var item in items.Where(item => !IsTerminal(item.Status)))
        {
            item.Status = BranchBackupStatus.Cancelled;
            item.FailureCode = DownloaderFailureCodes.DownloadCancelled;
            item.ErrorMessage = "The downloader operation was cancelled.";
            Notify(item, onItemChanged);
        }
    }

    private static void MarkFailed(
        IEnumerable<BranchBackupItem> items,
        string failureCode,
        string errorMessage,
        Action<BranchBackupItem>? onItemChanged,
        bool preserveReady = false)
    {
        foreach (var item in items.Where(item => !IsTerminal(item.Status)
            && (!preserveReady || item.Status != BranchBackupStatus.Ready)))
        {
            item.Status = BranchBackupStatus.Failed;
            item.FailureCode = failureCode;
            item.ErrorMessage = errorMessage;
            Notify(item, onItemChanged);
        }
    }

    private static bool IsZipForBranch(string fileName, string branchCode) =>
        DownloaderInputPolicy.IsSafeArchiveFileName(fileName)
        && fileName.StartsWith(branchCode + "_", StringComparison.OrdinalIgnoreCase);

    private static void Notify(BranchBackupItem item, Action<BranchBackupItem>? onItemChanged) => onItemChanged?.Invoke(item);

    private static string? ExtractSerial(string fileName)
    {
        var match = SerialRegex().Match(fileName);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"_(\d+)\.zip$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SerialRegex();
}

public sealed class SystemDownloaderDelay : IDownloaderDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
        Task.Delay(delay, cancellationToken);
}
