using System.IO.Compression;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RmsSupportHub.Pos.Application.Restore;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Services;

/// <summary>
/// Server-owned restore policy and execution orchestration. Archive metadata, content, and
/// destinations are treated as hostile until bounded validation has completed. The class never
/// trusts browser preview values and never writes a configuration or invokes SQL until the execute
/// caller has recomputed and compared the complete intent fingerprint.
/// </summary>
public sealed class RestoreService
{
    private const int CopyBufferSize = 128 * 1024;
    private const long MinimumFreeSpaceBytes = 8 * 1024 * 1024;
    private const long RestoreReserveBytes = 8 * 1024 * 1024;
    private const string LegacyArchiveVersion = "legacy-no-manifest-v1";
    private static readonly Regex SafeDatabaseIdentifierRegex = new("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IDatabaseService _databaseService;
    private readonly IRestoreFileSystem _fileSystem;
    private readonly IServiceManager? _serviceManager;
    private readonly IRestoreSqlPlanBuilder _sqlPlanBuilder;
    private readonly RestoreArchiveLimits _limits;
    private readonly TimeProvider _timeProvider;
    private readonly string _stagingRoot;

    /// <summary>Compatibility construction path used by the retained WinUI application.</summary>
    public RestoreService(IDatabaseService databaseService)
        : this(
            databaseService,
            new RestoreFileSystem(),
            null,
            new RestoreSqlPlanBuilder(),
            RestoreArchiveLimits.Default,
            TimeProvider.System)
    {
    }

    /// <summary>Agent composition path. All privileged I/O and service control are injected.</summary>
    public RestoreService(
        IDatabaseService databaseService,
        IRestoreFileSystem fileSystem,
        IServiceManager? serviceManager,
        IRestoreSqlPlanBuilder sqlPlanBuilder,
        RestoreArchiveLimits limits,
        TimeProvider timeProvider,
        string? stagingRoot = null)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _serviceManager = serviceManager;
        _sqlPlanBuilder = sqlPlanBuilder ?? throw new ArgumentNullException(nameof(sqlPlanBuilder));
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _limits.Validate();
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _stagingRoot = string.IsNullOrWhiteSpace(stagingRoot)
            ? Path.Combine(Path.GetTempPath(), "PosAdminTool", "restore")
            : _fileSystem.GetFullPath(stagingRoot);
    }

    public async Task<RestorePreviewBuildResult> BuildPreviewAsync(
        AppSettings settings,
        RestoreSourceDescriptor source,
        RestoreMode mode,
        CancellationToken cancellationToken = default)
    {
        RestorePreparedPlan? prepared = null;
        try
        {
            prepared = await PrepareAsync(settings, source, mode, cancellationToken).ConfigureAwait(false);
            return new RestorePreviewBuildResult(true, null, null, prepared.Intent, prepared.Warnings);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RestorePolicyException ex)
        {
            return new RestorePreviewBuildResult(false, ex.Code, ex.SafeMessage, null, []);
        }
        catch
        {
            return new RestorePreviewBuildResult(
                false,
                "restore.archive_invalid",
                "The restore source could not be validated.",
                null,
                []);
        }
        finally
        {
            if (prepared?.StagingDirectory is not null)
            {
                await DeleteDirectorySafeAsync(prepared.StagingDirectory).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Rebuilds the complete policy immediately before the destructive work. The expected
    /// fingerprint came from the server-side challenge, never from the client preview body.
    /// </summary>
    public async Task<RestoreExecutionResult> ExecuteAsync(
        AppSettings settings,
        RestoreSourceDescriptor source,
        RestoreMode mode,
        string expectedFingerprint,
        CancellationToken cancellationToken = default)
    {
        var operation = OperationResult.Running("restore_database");
        RestorePreparedPlan? prepared = null;
        try
        {
            prepared = await PrepareAsync(settings, source, mode, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(expectedFingerprint)
                || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(prepared.Intent.Fingerprint),
                    Encoding.UTF8.GetBytes(expectedFingerprint)))
            {
                operation.AddError("The restore preview is stale and must be recomputed.");
                operation.Finalize(OperationStatus.Failed);
                return new RestoreExecutionResult(operation, "restore.challenge_changed");
            }

            return await ExecutePreparedAsync(prepared, settings, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            operation.AddMessage("Restore cancelled.");
            operation.Finalize(OperationStatus.Cancelled);
            return new RestoreExecutionResult(operation, "restore.cancelled");
        }
        catch (RestorePolicyException ex)
        {
            operation.AddError(ex.SafeMessage);
            operation.Finalize(OperationStatus.Failed);
            return new RestoreExecutionResult(operation, ex.Code);
        }
        catch
        {
            operation.AddError("Restore failed while applying the validated server-owned plan.");
            operation.Finalize(OperationStatus.Failed);
            return new RestoreExecutionResult(operation, "restore.failed");
        }
        finally
        {
            if (prepared?.StagingDirectory is not null)
            {
                await DeleteDirectorySafeAsync(prepared.StagingDirectory).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Compatibility entry point for the retained WinUI workflow. Agent endpoints use the preview,
    /// challenge, and operation APIs instead.
    /// </summary>
    public async Task<OperationResult> RestoreAsync(
        AppSettings settings,
        string backupZip,
        string? targetDatabase = null,
        string? dbFilesPath = null,
        string restoreType = "Full",
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveSettings = settings.Clone();
        if (!string.IsNullOrWhiteSpace(targetDatabase))
        {
            effectiveSettings.Databases = [targetDatabase.Trim(), .. effectiveSettings.Databases.Where(database => !string.Equals(database, targetDatabase.Trim(), StringComparison.OrdinalIgnoreCase))];
        }

        if (!string.IsNullOrWhiteSpace(dbFilesPath)) effectiveSettings.DbFilesPath = dbFilesPath.Trim();
        var mode = NormalizeRestoreMode(restoreType);
        var source = new RestoreSourceDescriptor(
            new RestoreSourceReference(RestoreSourceKind.BrowseHandle, null, null, null, Path.GetFileName(backupZip)),
            backupZip);
        var preview = await BuildPreviewAsync(effectiveSettings, source, mode, cancellationToken).ConfigureAwait(false);
        if (!preview.Ready || preview.Intent is null)
        {
            var failed = OperationResult.Running("restore_database");
            failed.AddError(preview.SafeMessage ?? "Restore preview failed.");
            failed.Finalize(OperationStatus.Failed);
            return failed;
        }

        progress?.Report("Restore preview validated.");
        var execution = await ExecuteAsync(effectiveSettings, source, mode, preview.Intent.Fingerprint, cancellationToken).ConfigureAwait(false);
        if (execution.Operation.Status == OperationStatus.Success)
        {
            progress?.Report("Restore completed.");
        }
        else if (execution.Operation.Status == OperationStatus.PartialSuccess)
        {
            progress?.Report("Restore completed with a partial outcome.");
        }
        return execution.Operation;
    }

    private async Task<RestorePreparedPlan> PrepareAsync(
        AppSettings settings,
        RestoreSourceDescriptor source,
        RestoreMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(source);
        var stagingDirectory = (string?)null;
        try
        {
            var branchCode = ValidateBranchCode(settings.BranchCode);
            var targetDatabase = DatabaseResolver.ResolveBranchDatabase(settings).Trim();
            if (!SafeDatabaseIdentifierRegex.IsMatch(targetDatabase))
            {
                throw new RestorePolicyException("restore.sql_plan_invalid", "The configured target database is not supported.");
            }

            var extension = Path.GetExtension(source.Reference.DisplayName).ToLowerInvariant();
            var sourceIdentity = await ReadSourceIdentityAsync(
                source,
                extension == ".zip" ? _limits.MaxArchiveBytes : null,
                cancellationToken).ConfigureAwait(false);
            ArchiveInspection archive;
            if (extension == ".zip")
            {
                archive = await InspectZipAsync(source, sourceIdentity, branchCode, mode, cancellationToken).ConfigureAwait(false);
                stagingDirectory = archive.StagingDirectory;
            }
            else if (extension == ".bak")
            {
                if (mode == RestoreMode.ConfigOnly)
                {
                    throw new RestorePolicyException("restore.archive_bak_ambiguous", "A database backup is not valid for config-only restore.");
                }

                ValidateLegacyBakBranch(source.Reference.DisplayName, branchCode);
                archive = new ArchiveInspection(
                    "bare-bak-v1",
                    false,
                    source.CanonicalPath,
                    [],
                    [],
                    []);
            }
            else
            {
                throw new RestorePolicyException("restore.archive_extension_rejected", "Only a restore ZIP or device-side BAK source is supported.");
            }

            var warnings = new List<string>();
            warnings.AddRange(archive.Warnings);

            var logicalFiles = new List<RestoreFileInfo>();
            RestoreSqlPlan sqlPlan;
            if (mode is RestoreMode.Full or RestoreMode.DatabaseOnly)
            {
                if (string.IsNullOrWhiteSpace(archive.StagedBackupPath))
                {
                    throw new RestorePolicyException("restore.archive_bak_ambiguous", "The archive does not contain one usable database backup.");
                }

                IReadOnlyList<RestoreFileInfo> discovered;
                try
                {
                    discovered = await _databaseService.ReadRestoreFileListAsync(settings, archive.StagedBackupPath, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    throw new RestorePolicyException("restore.sql_inspection_failed", "The database backup could not be inspected.");
                }

                if (discovered is null || discovered.Count == 0)
                {
                    throw new RestorePolicyException(
                        RestoreFailureCodes.SqlInspectionFailed,
                        "The database backup did not provide usable logical-file evidence.");
                }

                if (discovered.Count > _limits.MaxLogicalFiles)
                {
                    throw new RestorePolicyException("restore.sql_plan_invalid", "The database backup contains too many logical files.");
                }

                logicalFiles.AddRange(discovered);
                var dbFilesPath = ValidateDestinationDirectory(settings.DbFilesPath, "The database file destination is not supported.");
                try
                {
                    sqlPlan = _sqlPlanBuilder.Build(targetDatabase, dbFilesPath, logicalFiles);
                }
                catch (RestorePolicyException)
                {
                    throw;
                }
                catch
                {
                    throw new RestorePolicyException("restore.sql_plan_invalid", "The SQL MOVE plan could not be created.");
                }
            }
            else
            {
                sqlPlan = new RestoreSqlPlan(targetDatabase, string.Empty, []);
            }

            var configTargets = BuildConfigTargets(settings, archive.ConfigEntries, mode);
            if (mode == RestoreMode.Full && configTargets.Count == 0)
            {
                warnings.Add("No recognized RMS configuration files were present in the archive.");
            }

            var serviceTargets = BuildServiceTargets(settings.Services);
            var requiredFreeSpace = CalculateRequiredFreeSpace(mode, archive.StagedBackupPath, configTargets);
            var availableFreeSpace = mode is RestoreMode.Full or RestoreMode.DatabaseOnly
                ? ReadAvailableFreeSpace(settings.DbFilesPath)
                : long.MaxValue;
            if (availableFreeSpace < requiredFreeSpace)
            {
                throw new RestorePolicyException("restore.insufficient_space", "The server-owned restore destination does not have enough free space.");
            }

            var confirmationText = $"RESTORE {targetDatabase}";
            var fingerprint = ComputeFingerprint(
                source,
                sourceIdentity,
                mode,
                branchCode,
                targetDatabase,
                archive.ArchiveVersion,
                logicalFiles,
                sqlPlan,
                configTargets,
                serviceTargets,
                requiredFreeSpace,
                _limits);
            var intent = new RestorePreviewIntent(
                source.Reference,
                sourceIdentity,
                mode,
                branchCode,
                targetDatabase,
                archive.ArchiveVersion,
                logicalFiles,
                sqlPlan,
                configTargets,
                serviceTargets,
                requiredFreeSpace,
                confirmationText,
                fingerprint);

            return new RestorePreparedPlan(intent, archive.StagingDirectory, archive.StagedBackupPath, warnings);
        }
        catch
        {
            if (stagingDirectory is not null)
            {
                await DeleteDirectorySafeAsync(stagingDirectory).ConfigureAwait(false);
            }

            throw;
        }
    }

    private async Task<RestoreExecutionResult> ExecutePreparedAsync(
        RestorePreparedPlan prepared,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var operation = OperationResult.Running("restore_database");
        var stoppedServices = new List<RestoreServiceTarget>();
        var milestones = new RestoreExecutionMilestones();
        RestorePolicyException? failure = null;
        var cancelled = false;
        var restartFailed = false;

        try
        {
            operation.AddMessage("Restore policy revalidation completed.");
            operation.AddMessage("Stopping affected services.");
            await StopServicesAsync(prepared.Intent.Services, stoppedServices, cancellationToken).ConfigureAwait(false);
            milestones.ServicesStopped = stoppedServices.Count > 0;
            if (milestones.ServicesStopped)
            {
                operation.AddMessage("Affected services stopped successfully.");
            }

            if (prepared.Intent.Mode is RestoreMode.Full or RestoreMode.DatabaseOnly)
            {
                cancellationToken.ThrowIfCancellationRequested();
                operation.AddMessage("Restoring the validated database backup.");
                milestones.DatabaseRestoreAttempted = true;
                await _databaseService.RestoreDatabaseAsync(
                    settings,
                    prepared.Intent.TargetDatabase,
                    prepared.StagedBackupPath!,
                    prepared.Intent.LogicalFiles,
                    prepared.Intent.SqlPlan.DbFilesPath,
                    cancellationToken).ConfigureAwait(false);
                milestones.DatabaseRestoreCompleted = true;
                operation.AddMessage("Database restore completed.");

                if (_databaseService is IDatabaseRestoreVerifier verifier
                    && !await verifier.VerifyRestoreAsync(settings, prepared.Intent.TargetDatabase, cancellationToken).ConfigureAwait(false))
                {
                    throw new RestorePolicyException("restore.verification_failed", "Post-restore verification did not confirm the target database.");
                }

                if (_databaseService is IDatabaseRestoreVerifier)
                {
                    milestones.PostDatabaseVerificationCompleted = true;
                    operation.AddMessage("Post-database verification completed.");
                }
            }

            if (prepared.Intent.Mode is RestoreMode.Full or RestoreMode.ConfigOnly)
            {
                cancellationToken.ThrowIfCancellationRequested();
                milestones.ConfigurationOverwriteStarted = prepared.Intent.ConfigTargets.Count > 0;
                if (milestones.ConfigurationOverwriteStarted)
                {
                    operation.AddMessage("Applying validated configuration overwrites.");
                }

                var configuration = await ApplyConfigurationAsync(prepared, cancellationToken).ConfigureAwait(false);
                milestones.ConfigurationOverwriteStarted |= configuration.OverwriteStarted;
                milestones.ConfigurationOverwriteCompleted = configuration.OverwriteCompleted;
                milestones.ConfigurationRollbackAttempted = configuration.RollbackAttempted;
                milestones.ConfigurationRollbackCompleted = configuration.RollbackCompleted;

                switch (configuration.Status)
                {
                    case ConfigurationApplyStatus.Completed when configuration.OverwriteCompleted:
                        operation.AddMessage("Configuration overwrite completed.");
                        break;
                    case ConfigurationApplyStatus.Failed:
                        if (configuration.RollbackAttempted && configuration.RollbackCompleted)
                        {
                            operation.AddMessage("Configuration overwrite did not complete; the previous configuration was restored.");
                        }

                        failure = new RestorePolicyException(
                            RestoreFailureCodes.ConfigCopyFailed,
                            "Configuration files could not be restored safely.");
                        break;
                    case ConfigurationApplyStatus.Cancelled:
                        cancelled = true;
                        if (configuration.RollbackAttempted && configuration.RollbackCompleted)
                        {
                            operation.AddMessage("Configuration rollback completed after cancellation.");
                        }

                        break;
                    case ConfigurationApplyStatus.RollbackFailed:
                        operation.AddMessage("Configuration rollback did not complete; manual recovery is required.");
                        failure = new RestorePolicyException(
                            RestoreFailureCodes.ConfigRollbackFailed,
                            "The previous configuration could not be completely restored. Manual recovery is required.");
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (RestorePolicyException ex)
        {
            failure = ex;
        }
        catch
        {
            failure = new RestorePolicyException("restore.failed", "Restore execution failed.");
        }
        finally
        {
            foreach (var service in stoppedServices.AsEnumerable().Reverse())
            {
                try
                {
                    if (_serviceManager is not null)
                    {
                        await _serviceManager.ControlAsync(service.ServiceName, ServiceControlAction.Start, CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch
                {
                    restartFailed = true;
                }
            }

            milestones.ServicesStopped |= stoppedServices.Count > 0;
            milestones.ServicesRestarted = stoppedServices.Count == 0 || !restartFailed;
        }

        if (cancelled)
        {
            if (HasDestructiveOutcome(milestones, restartFailed))
            {
                if (DatabaseRestoreInterrupted(milestones))
                {
                    operation.AddError(RestoreFailureCodes.DatabaseRestoreInterruptedMessage);
                    operation.AddMessage("Database state must be verified before further operations.");
                }
                else
                {
                    operation.AddError("Destructive restore work completed before cancellation; the requested restore remains incomplete.");
                    operation.AddMessage("Cancellation did not reverse completed destructive restore work.");
                }

                if (restartFailed)
                {
                    operation.AddError("One or more affected services could not be restarted; manual recovery is required.");
                }

                operation.Finalize(OperationStatus.PartialSuccess);
                return new RestoreExecutionResult(
                    operation,
                    ResolvePartialFailureCode(null, milestones, cancelled: true, restartFailed: restartFailed));
            }

            operation.AddMessage("Restore cancelled.");
            operation.Finalize(OperationStatus.Cancelled);
            return new RestoreExecutionResult(operation, "restore.cancelled");
        }

        if (failure is not null)
        {
            operation.AddError(DatabaseRestoreInterrupted(milestones)
                ? RestoreFailureCodes.DatabaseRestoreInterruptedMessage
                : failure.SafeMessage);
            if (restartFailed)
            {
                operation.AddError("One or more affected services could not be restarted; manual recovery is required.");
            }

            if (HasDestructiveOutcome(milestones, restartFailed))
            {
                operation.AddMessage(DatabaseRestoreInterrupted(milestones)
                    ? "Database state must be verified before further operations."
                    : "The restore changed destructive state but did not achieve the requested complete outcome.");
                operation.Finalize(OperationStatus.PartialSuccess);
                return new RestoreExecutionResult(
                    operation,
                    ResolvePartialFailureCode(failure, milestones, cancelled: false, restartFailed: restartFailed));
            }

            operation.Finalize(OperationStatus.Failed);
            return new RestoreExecutionResult(operation, failure.Code);
        }

        if (restartFailed)
        {
            operation.AddError("One or more affected services could not be restarted; manual recovery is required.");
            operation.AddMessage("The restore completed only partially because service recovery was incomplete.");
            operation.Finalize(OperationStatus.PartialSuccess);
            return new RestoreExecutionResult(operation, RestoreFailureCodes.ServiceRestartFailed);
        }

        operation.Context["target_database"] = prepared.Intent.TargetDatabase;
        operation.Context["restore_mode"] = prepared.Intent.Mode.ToString();
        operation.AddMessage(prepared.Intent.Mode switch
        {
            RestoreMode.ConfigOnly => "Configuration restore completed successfully.",
            RestoreMode.DatabaseOnly => "Database restore completed successfully.",
            _ => "Full restore completed successfully."
        });
        operation.Finalize(OperationStatus.Success);
        return new RestoreExecutionResult(operation);
    }

    private static bool HasDestructiveOutcome(RestoreExecutionMilestones milestones, bool restartFailed) =>
        milestones.DatabaseRestoreAttempted
        || (milestones.ConfigurationRollbackAttempted && !milestones.ConfigurationRollbackCompleted)
        || (milestones.ServicesStopped && restartFailed);

    private static bool DatabaseRestoreInterrupted(RestoreExecutionMilestones milestones) =>
        milestones.DatabaseRestoreAttempted && !milestones.DatabaseRestoreCompleted;

    private static string ResolvePartialFailureCode(
        RestorePolicyException? failure,
        RestoreExecutionMilestones milestones,
        bool cancelled,
        bool restartFailed)
    {
        if (DatabaseRestoreInterrupted(milestones)) return RestoreFailureCodes.DatabaseRestoreInterrupted;
        if (restartFailed) return RestoreFailureCodes.ServiceRestartFailed;
        if (milestones.ConfigurationRollbackAttempted && !milestones.ConfigurationRollbackCompleted)
            return RestoreFailureCodes.ConfigRollbackFailed;
        if (cancelled) return RestoreFailureCodes.CancelledAfterDestructiveWork;
        if (milestones.DatabaseRestoreCompleted && failure?.Code == RestoreFailureCodes.ConfigCopyFailed)
            return RestoreFailureCodes.PartialFailure;
        return failure?.Code ?? RestoreFailureCodes.PartialFailure;
    }

    private async Task StopServicesAsync(
        IReadOnlyList<RestoreServiceTarget> services,
        List<RestoreServiceTarget> stoppedServices,
        CancellationToken cancellationToken)
    {
        if (_serviceManager is null || services.Count == 0) return;

        IReadOnlyDictionary<string, ServiceStatus> statuses;
        try
        {
            statuses = await _serviceManager.GetStatusesAsync(services.Select(service => service.ServiceName), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            throw new RestorePolicyException("restore.service_stop_failed", "Affected services could not be inspected.");
        }

        foreach (var service in services)
        {
            if (!statuses.TryGetValue(service.ServiceName, out var status) || status is not ServiceStatus.Running)
            {
                continue;
            }

            try
            {
                await _serviceManager.ControlAsync(service.ServiceName, ServiceControlAction.Stop, cancellationToken).ConfigureAwait(false);
                stoppedServices.Add(service);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new RestorePolicyException("restore.service_stop_failed", "An affected service could not be stopped safely.");
            }
        }
    }

    private async Task<ConfigurationApplyResult> ApplyConfigurationAsync(
        RestorePreparedPlan prepared,
        CancellationToken cancellationToken)
    {
        if (prepared.Intent.ConfigTargets.Count == 0)
        {
            return new(ConfigurationApplyStatus.Completed, false, false, false, true);
        }

        if (prepared.StagingDirectory is null) throw new RestorePolicyException("restore.config_copy_failed", "The configuration staging area is unavailable.");

        var rollbackDirectory = Path.Combine(prepared.StagingDirectory, "rollback");
        var rollback = new List<RollbackFile>();
        var overwriteStarted = false;
        try
        {
            await _fileSystem.EnsureDirectoryAsync(rollbackDirectory, cancellationToken).ConfigureAwait(false);
            EnsureSafeDirectoryChain(rollbackDirectory, "The configuration staging area is unsafe.");

            foreach (var target in prepared.Intent.ConfigTargets)
            {
                EnsureSafeFilePath(target.DestinationPath, "The configuration destination is not supported.");
                EnsureSafeFilePath(target.StagedSourcePath, "The staged configuration is not supported.");
                var parent = Path.GetDirectoryName(target.DestinationPath);
                if (string.IsNullOrWhiteSpace(parent)) throw new RestorePolicyException("restore.destination_unsafe", "The configuration destination is not supported.");
                await _fileSystem.EnsureDirectoryAsync(parent, cancellationToken).ConfigureAwait(false);
                EnsureSafeFilePath(target.DestinationPath, "The configuration destination is not supported.");

                var backupPath = Path.Combine(rollbackDirectory, $"{target.TargetId}.json.bak");
                var existed = _fileSystem.FileExists(target.DestinationPath);
                if (existed)
                {
                    await _fileSystem.CopyFileAtomicAsync(target.DestinationPath, backupPath, cancellationToken).ConfigureAwait(false);
                }

                rollback.Add(new RollbackFile(target.DestinationPath, backupPath, existed));
            }

            overwriteStarted = true;
            foreach (var target in prepared.Intent.ConfigTargets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _fileSystem.CopyFileAtomicAsync(target.StagedSourcePath, target.DestinationPath, cancellationToken).ConfigureAwait(false);
            }

            return new(ConfigurationApplyStatus.Completed, true, true, false, true);
        }
        catch (OperationCanceledException)
        {
            var rollbackResult = await RollbackConfigurationAsync(rollback).ConfigureAwait(false);
            return new(
                rollbackResult.Completed ? ConfigurationApplyStatus.Cancelled : ConfigurationApplyStatus.RollbackFailed,
                overwriteStarted,
                false,
                true,
                rollbackResult.Completed);
        }
        catch
        {
            var rollbackResult = await RollbackConfigurationAsync(rollback).ConfigureAwait(false);
            return new(
                rollbackResult.Completed ? ConfigurationApplyStatus.Failed : ConfigurationApplyStatus.RollbackFailed,
                overwriteStarted,
                false,
                true,
                rollbackResult.Completed);
        }
        finally
        {
            try { await _fileSystem.DeleteDirectoryAsync(rollbackDirectory, CancellationToken.None).ConfigureAwait(false); } catch { }
        }
    }

    private async Task<RollbackResult> RollbackConfigurationAsync(IReadOnlyList<RollbackFile> rollback)
    {
        var completed = true;
        foreach (var item in rollback.Reverse())
        {
            try
            {
                if (item.Existed)
                {
                    if (!_fileSystem.FileExists(item.BackupPath)) throw new IOException("Rollback evidence is unavailable.");
                    await _fileSystem.CopyFileAtomicAsync(item.BackupPath, item.DestinationPath, CancellationToken.None).ConfigureAwait(false);
                }
                else if (!item.Existed)
                {
                    await _fileSystem.DeleteFileAsync(item.DestinationPath, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch
            {
                completed = false;
            }
        }

        return new(completed);
    }

    private async Task<ArchiveInspection> InspectZipAsync(
        RestoreSourceDescriptor source,
        RestoreSourceIdentity sourceIdentity,
        string branchCode,
        RestoreMode mode,
        CancellationToken cancellationToken)
    {
        if (sourceIdentity.SizeBytes > _limits.MaxArchiveBytes)
        {
            throw new RestorePolicyException("restore.archive_invalid", "The restore archive is larger than the bounded inspection limit.");
        }

        await ValidateZipCentralDirectoryAsync(source.CanonicalPath, sourceIdentity.SizeBytes, cancellationToken).ConfigureAwait(false);
        await using var sourceStream = await _fileSystem.OpenReadAsync(source.CanonicalPath, cancellationToken).ConfigureAwait(false);
        using var archive = new ZipArchive(sourceStream, ZipArchiveMode.Read, leaveOpen: false);
        var entries = new List<ArchiveEntryInfo>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expandedBytes = 0;
        ZipArchiveEntry? manifestEntry = null;

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entries.Count >= _limits.MaxArchiveEntryCount)
            {
                throw new RestorePolicyException("restore.archive_entry_limit", "The restore archive contains too many entries.");
            }

            var name = ValidateArchiveEntryName(entry.FullName);
            if (!names.Add(name))
            {
                throw new RestorePolicyException("restore.archive_duplicate_entry", "The restore archive contains duplicate entry names.");
            }

            if (entry.Length < 0 || entry.CompressedLength < 0)
            {
                throw new RestorePolicyException("restore.archive_invalid", "The restore archive contains invalid size metadata.");
            }

            expandedBytes = checked(expandedBytes + entry.Length);
            if (expandedBytes > _limits.MaxExpandedBytes)
            {
                throw new RestorePolicyException("restore.archive_expanded_size_limit", "The restore archive expands beyond the bounded inspection limit.");
            }

            if (entry.Length > 0
                && (entry.CompressedLength == 0 || (double)entry.Length / entry.CompressedLength > _limits.MaxCompressionRatio))
            {
                throw new RestorePolicyException("restore.archive_compression_ratio", "The restore archive compression characteristics are unsafe.");
            }

            var unixMode = ((uint)entry.ExternalAttributes >> 16) & 0xF000;
            if (unixMode == 0xA000)
            {
                throw new RestorePolicyException("restore.archive_path_rejected", "The restore archive contains a symbolic-link entry.");
            }

            if (name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                manifestEntry = entry;
            }
            else if (!name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)
                && !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                throw new RestorePolicyException("restore.archive_extension_rejected", "The restore archive contains an unsupported file type.");
            }

            entries.Add(new ArchiveEntryInfo(name, entry, entry.Length, entry.CompressedLength));
        }

        var bakEntries = entries.Where(entry => entry.Name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)).ToList();
        if (bakEntries.Count > 1 || (mode is RestoreMode.Full or RestoreMode.DatabaseOnly) && bakEntries.Count != 1)
        {
            throw new RestorePolicyException("restore.archive_bak_ambiguous", "The restore archive must contain exactly one database backup.");
        }

        var configEntries = new List<ArchiveConfigEntry>();
        var configTargetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.Where(entry => entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            if (entry.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)) continue;
            var targetId = ResolveConfigTargetId(entry.Name);
            if (targetId is null)
            {
                throw new RestorePolicyException("restore.archive_unknown_json", "The restore archive contains an unrecognized JSON file.");
            }

            if (!configTargetIds.Add(targetId))
            {
                throw new RestorePolicyException("restore.archive_duplicate_entry", "The restore archive contains ambiguous configuration mappings.");
            }

            configEntries.Add(new ArchiveConfigEntry(targetId, entry.Name, string.Empty, entry.Length, string.Empty));
        }

        var digests = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.Where(entry => !entry.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)))
        {
            digests[entry.Name] = await ComputeEntryChecksumAsync(entry.Entry, entry.Length, cancellationToken).ConfigureAwait(false);
        }

        var warnings = new List<string>();
        string archiveVersion;
        if (manifestEntry is null)
        {
            archiveVersion = LegacyArchiveVersion;
            warnings.Add("The legacy restore archive has no manifest; bounded filename and content policy was applied.");
            ValidateLegacyArchiveBranch(branchCode, source.Reference.DisplayName, bakEntries);
        }
        else
        {
            var manifest = await ReadManifestAsync(manifestEntry, cancellationToken).ConfigureAwait(false);
            archiveVersion = $"manifest-v{manifest.SchemaVersion}";
            if (!string.Equals(manifest.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new RestorePolicyException("restore.archive_branch_mismatch", "The restore archive belongs to a different branch.");
            }

            var nonManifestNames = entries
                .Where(entry => !entry.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (manifest.Contents.Count != nonManifestNames.Count
                || manifest.Contents.Any(item => !nonManifestNames.Contains(item.ArchiveName)))
            {
                throw new RestorePolicyException("restore.archive_manifest_invalid", "The restore archive manifest does not describe the archive contents.");
            }

            foreach (var item in manifest.Contents)
            {
                if (!digests.TryGetValue(item.ArchiveName, out var actualChecksum)
                    || item.SizeBytes != entries.Single(entry => entry.Name.Equals(item.ArchiveName, StringComparison.OrdinalIgnoreCase)).Length
                    || !string.Equals(item.Sha256Checksum, actualChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new RestorePolicyException("restore.archive_checksum_mismatch", "The restore archive checksum evidence does not match its contents.");
                }

                var entry = entries.Single(candidate => candidate.Name.Equals(item.ArchiveName, StringComparison.OrdinalIgnoreCase));
                var manifestTargetId = ResolveManifestTargetId(item.ComponentId);
                var configTargetId = ResolveConfigTargetId(entry.Name);
                if (configTargetId is not null)
                {
                    if (!string.Equals(configTargetId, manifestTargetId, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new RestorePolicyException("restore.archive_manifest_invalid", "The restore archive manifest maps a configuration file ambiguously.");
                    }
                }
                else if (manifestTargetId is not null)
                {
                    throw new RestorePolicyException("restore.archive_manifest_invalid", "The restore archive manifest maps the database backup ambiguously.");
                }
            }
        }

        if (mode == RestoreMode.ConfigOnly && configEntries.Count == 0)
        {
            throw new RestorePolicyException("restore.archive_unknown_json", "The restore archive contains no recognized configuration file.");
        }

        var stagingDirectory = await CreateStagingDirectoryAsync(cancellationToken).ConfigureAwait(false);
        var expectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var info in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var targetPath = Path.Combine(stagingDirectory, info.Name);
                var canonicalTarget = _fileSystem.GetFullPath(targetPath);
                if (!IsWithinDirectory(stagingDirectory, canonicalTarget))
                {
                    throw new RestorePolicyException("restore.archive_path_rejected", "The restore archive entry escapes its staging area.");
                }

                expectedPaths.Add(canonicalTarget);
                await using var input = info.Entry.Open();
                await using var output = await _fileSystem.CreateFileAsync(canonicalTarget, cancellationToken).ConfigureAwait(false);
                await CopyBoundedAsync(input, output, info.Length, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                var extractedLength = _fileSystem.GetFileLength(canonicalTarget);
                if (extractedLength != info.Length)
                {
                    throw new RestorePolicyException("restore.archive_invalid", "The extracted restore content size changed unexpectedly.");
                }

                if (_fileSystem.IsReparsePoint(canonicalTarget))
                {
                    throw new RestorePolicyException("restore.archive_path_rejected", "The extracted restore content is not a regular file.");
                }
            }

            foreach (var actual in _fileSystem.EnumerateFileSystemEntries(stagingDirectory))
            {
                var canonicalActual = _fileSystem.GetFullPath(actual);
                if (_fileSystem.IsReparsePoint(canonicalActual) || !expectedPaths.Contains(canonicalActual))
                {
                    throw new RestorePolicyException("restore.archive_path_rejected", "The extracted restore content changed unexpectedly.");
                }
            }

            var stagedBak = bakEntries.Count == 1
                ? Path.Combine(stagingDirectory, bakEntries[0].Name)
                : string.Empty;
            var stagedConfigs = configEntries
                .Select(config => config with
                {
                    StagedSourcePath = Path.Combine(stagingDirectory, config.ArchiveEntryName),
                    Sha256Checksum = digests[config.ArchiveEntryName]
                })
                .ToList();
            return new ArchiveInspection(archiveVersion, manifestEntry is not null, stagedBak, stagedConfigs, warnings, [.. entries])
            {
                StagingDirectory = stagingDirectory,
            };
        }
        catch
        {
            await DeleteDirectorySafeAsync(stagingDirectory).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<RestoreSourceIdentity> ReadSourceIdentityAsync(
        RestoreSourceDescriptor source,
        long? maximumSizeBytes,
        CancellationToken cancellationToken)
    {
        string canonicalPath;
        try
        {
            canonicalPath = _fileSystem.GetFullPath(source.CanonicalPath);
        }
        catch
        {
            throw new RestorePolicyException("restore.source_invalid", "The restore source is invalid.");
        }

        EnsureSafeExistingFile(canonicalPath, "The restore source is unavailable or unsafe.");
        long size;
        DateTimeOffset lastWrite;
        string checksum;
        try
        {
            size = _fileSystem.GetFileLength(canonicalPath);
            if (maximumSizeBytes.HasValue && size > maximumSizeBytes.Value)
            {
                throw new RestorePolicyException("restore.archive_invalid", "The restore archive is larger than the bounded inspection limit.");
            }

            lastWrite = _fileSystem.GetLastWriteTimeUtc(canonicalPath);
            checksum = await _fileSystem.ComputeSha256Async(canonicalPath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RestorePolicyException)
        {
            throw;
        }
        catch
        {
            throw new RestorePolicyException("restore.source_invalid", "The restore source could not be read safely.");
        }

        return new RestoreSourceIdentity(canonicalPath, size, lastWrite, checksum);
    }

    private async Task<string> CreateStagingDirectoryAsync(CancellationToken cancellationToken)
    {
        EnsureSafeDirectoryChain(_stagingRoot, "The restore staging area is unsafe.");
        await _fileSystem.EnsureDirectoryAsync(_stagingRoot, cancellationToken).ConfigureAwait(false);
        var stagingDirectory = Path.Combine(_stagingRoot, Guid.NewGuid().ToString("N"));
        try
        {
            await _fileSystem.EnsureDirectoryAsync(stagingDirectory, cancellationToken).ConfigureAwait(false);
            EnsureSafeDirectoryChain(stagingDirectory, "The restore staging area is unsafe.");
            return stagingDirectory;
        }
        catch
        {
            await DeleteDirectorySafeAsync(stagingDirectory).ConfigureAwait(false);
            throw;
        }
    }

    private void EnsureSafeExistingFile(string path, string message)
    {
        EnsureSafePathSyntax(path, message);
        if (!_fileSystem.FileExists(path) || _fileSystem.IsReparsePoint(path))
        {
            throw new RestorePolicyException("restore.source_invalid", message);
        }

        EnsureSafeDirectoryChain(Path.GetDirectoryName(path) ?? path, message);
    }

    private string ValidateDestinationDirectory(string path, string message)
    {
        EnsureSafePathSyntax(path, message);
        var canonical = _fileSystem.GetFullPath(path);
        if (!_fileSystem.DirectoryExists(canonical))
        {
            throw new RestorePolicyException("restore.destination_unsafe", message);
        }

        EnsureSafeDirectoryChain(canonical, message);
        return canonical;
    }

    private void EnsureSafeFilePath(string path, string message)
    {
        EnsureSafePathSyntax(path, message);
        var canonical = _fileSystem.GetFullPath(path);
        if (_fileSystem.FileExists(canonical) && _fileSystem.IsReparsePoint(canonical))
        {
            throw new RestorePolicyException("restore.destination_unsafe", message);
        }

        EnsureSafeDirectoryChain(Path.GetDirectoryName(canonical) ?? canonical, message);
    }

    private void EnsureSafePathSyntax(string path, string message)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || path.Contains('%') || path.Contains('\0'))
        {
            throw new RestorePolicyException("restore.destination_unsafe", message);
        }

        var segments = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is ".." or "."))
        {
            throw new RestorePolicyException("restore.destination_unsafe", message);
        }
    }

    private void EnsureSafeDirectoryChain(string path, string message)
    {
        string current;
        try { current = _fileSystem.GetFullPath(path); }
        catch { throw new RestorePolicyException("restore.destination_unsafe", message); }

        while (!string.IsNullOrWhiteSpace(current))
        {
            if (_fileSystem.IsReparsePoint(current))
            {
                throw new RestorePolicyException("restore.destination_unsafe", message);
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent;
        }
    }

    private long ReadAvailableFreeSpace(string path)
    {
        try { return Math.Max(0, _fileSystem.GetAvailableFreeSpace(path)); }
        catch { throw new RestorePolicyException("restore.destination_unsafe", "The restore destination could not be inspected safely."); }
    }

    private List<RestoreConfigTarget> BuildConfigTargets(
        AppSettings settings,
        IReadOnlyList<ArchiveConfigEntry> entries,
        RestoreMode mode)
    {
        if (mode == RestoreMode.DatabaseOnly) return [];

        var destinations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["branch-config"] = settings.BranchConfigPath,
            ["cashier-server-config"] = settings.CashierGrpcConfigPath,
            ["cashier-ui-config"] = settings.CashierUiConfigPath,
        };
        var result = new List<RestoreConfigTarget>();
        foreach (var entry in entries)
        {
            if (!destinations.TryGetValue(entry.TargetId, out var destination))
            {
                throw new RestorePolicyException("restore.destination_unsafe", "A configuration destination is not server-owned.");
            }

            EnsureSafeFilePath(destination, "A configuration destination is unsafe.");
            var canonical = _fileSystem.GetFullPath(destination);
            result.Add(new RestoreConfigTarget(
                entry.TargetId,
                entry.ArchiveEntryName,
                canonical,
                Path.GetFileName(canonical),
                entry.StagedSourcePath));
        }

        if (mode == RestoreMode.ConfigOnly && result.Count == 0)
        {
            throw new RestorePolicyException("restore.archive_unknown_json", "The restore archive contains no recognized configuration file.");
        }

        return result;
    }

    private static IReadOnlyList<RestoreServiceTarget> BuildServiceTargets(IEnumerable<string> configuredServices)
    {
        var result = new List<RestoreServiceTarget>();
        foreach (var serviceName in configuredServices ?? [])
        {
            var trimmed = serviceName?.Trim() ?? string.Empty;
            if (trimmed.Length == 0 || trimmed.Length > 128) continue;
            if (result.Any(existing => string.Equals(existing.ServiceName, trimmed, StringComparison.OrdinalIgnoreCase))) continue;
            var id = "svc-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trimmed))).ToLowerInvariant()[..16];
            result.Add(new RestoreServiceTarget(id, trimmed));
        }

        return result;
    }

    private long CalculateRequiredFreeSpace(
        RestoreMode mode,
        string? stagedBackupPath,
        IReadOnlyList<RestoreConfigTarget> configTargets)
    {
        try
        {
            long required = mode is RestoreMode.Full or RestoreMode.DatabaseOnly ? MinimumFreeSpaceBytes : 0;
            if (stagedBackupPath is not null && stagedBackupPath.Length > 0)
            {
                required = checked(required + _fileSystem.GetFileLength(stagedBackupPath) + RestoreReserveBytes);
            }

            foreach (var config in configTargets)
            {
                required = checked(required + Math.Max(0, _fileSystem.GetFileLength(config.StagedSourcePath)));
            }

            return required;
        }
        catch (RestorePolicyException)
        {
            throw;
        }
        catch
        {
            throw new RestorePolicyException("restore.destination_unsafe", "Restore space requirements could not be calculated safely.");
        }
    }

    private static string ComputeFingerprint(
        RestoreSourceDescriptor source,
        RestoreSourceIdentity sourceIdentity,
        RestoreMode mode,
        string branchCode,
        string targetDatabase,
        string archiveVersion,
        IReadOnlyList<RestoreFileInfo> logicalFiles,
        RestoreSqlPlan sqlPlan,
        IReadOnlyList<RestoreConfigTarget> configTargets,
        IReadOnlyList<RestoreServiceTarget> services,
        long requiredFreeSpace,
        RestoreArchiveLimits limits)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            policy = new
            {
                version = RestoreArchiveLimits.PolicyVersion,
                limits.MaxArchiveEntryCount,
                limits.MaxArchiveBytes,
                limits.MaxExpandedBytes,
                limits.MaxCompressionRatio,
                limits.MaxManifestBytes,
                limits.MaxLogicalFiles,
            },
            source = new
            {
                kind = source.Reference.Kind.ToString(),
                source.Reference.BrowseRootId,
                source.Reference.RelativeSubPath,
                source.Reference.UploadId,
                source.Reference.DisplayName,
                sourceIdentity.CanonicalPath,
                sourceIdentity.SizeBytes,
                sourceIdentity.LastWriteTimeUtc,
                sourceIdentity.Sha256Checksum,
            },
            mode,
            branchCode,
            targetDatabase,
            archiveVersion,
            logicalFiles,
            moves = sqlPlan.Moves.Select(move => new { move.LogicalName, move.FileType, move.DestinationPath }).ToList(),
            config = configTargets.Select(config => new { config.TargetId, config.ArchiveEntryName, config.DestinationPath }).ToList(),
            services = services.Select(service => new { service.ServiceId, service.ServiceName }).ToList(),
            requiredFreeSpace,
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static RestoreMode NormalizeRestoreMode(string restoreType)
    {
        var normalized = string.Join(' ', (restoreType ?? "Full").Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized switch
        {
            "database only" or "database-only" => RestoreMode.DatabaseOnly,
            "config only" or "config-only" => RestoreMode.ConfigOnly,
            _ => RestoreMode.Full,
        };
    }

    private static string ValidateBranchCode(string branchCode)
    {
        var value = branchCode?.Trim() ?? string.Empty;
        if (value.Length == 0 || value.Length > 50 || value.Contains('\0'))
        {
            throw new RestorePolicyException("restore.archive_branch_mismatch", "A configured branch identity is required.");
        }

        return value;
    }

    private static void ValidateLegacyBakBranch(string fileName, string branchCode)
    {
        var tokens = ExtractLegacyBranchTokens([fileName]);
        if (tokens.Count == 0)
        {
            throw new RestorePolicyException(
                RestoreFailureCodes.BranchEvidenceMissing,
                "The device backup filename does not contain trusted branch evidence.");
        }

        if (tokens.Count != 1)
        {
            throw new RestorePolicyException(
                "restore.archive_bak_ambiguous",
                "The device backup filename contains ambiguous branch evidence.");
        }

        if (!string.Equals(tokens[0], branchCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new RestorePolicyException(
                "restore.archive_branch_mismatch",
                "The device backup belongs to a different branch.");
        }
    }

    private static void ValidateLegacyArchiveBranch(string branchCode, string sourceName, IReadOnlyList<ArchiveEntryInfo> bakEntries)
    {
        ValidateLegacyBranchTokens(bakEntries.Select(entry => entry.Name).Append(sourceName), branchCode);
    }

    private static void ValidateLegacyBranchTokens(IEnumerable<string> names, string branchCode)
    {
        foreach (var token in ExtractLegacyBranchTokens(names))
        {
            if (!string.Equals(token, branchCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new RestorePolicyException("restore.archive_branch_mismatch", "The restore archive belongs to a different branch.");
            }
        }
    }

    private static IReadOnlyList<string> ExtractLegacyBranchTokens(IEnumerable<string> names) =>
        names.SelectMany(name => Regex.Matches(name ?? string.Empty, "(?i)(?<![A-Z0-9])B\\d{1,10}(?![A-Z0-9])")
                .Cast<Match>()
                .Select(match => match.Value))
            .ToList();

    private static string ValidateArchiveEntryName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName) || rawName.Contains('\0'))
        {
            throw new RestorePolicyException("restore.archive_path_rejected", "The restore archive contains an invalid entry path.");
        }

        var normalized = rawName.Replace('\\', '/');
        if (normalized.StartsWith('/')
            || normalized.StartsWith("//", StringComparison.Ordinal)
            || Regex.IsMatch(normalized, "^[A-Za-z]:", RegexOptions.CultureInvariant))
        {
            throw new RestorePolicyException("restore.archive_path_rejected", "The restore archive contains an absolute entry path.");
        }

        var segments = normalized.Split('/', StringSplitOptions.None);
        if (segments.Length != 1 || segments.Any(segment => segment is "" or "." or "..") || normalized.Contains(':'))
        {
            throw new RestorePolicyException("restore.archive_path_rejected", "The restore archive contains a traversal or nested entry path.");
        }

        return normalized;
    }

    private static string? ResolveConfigTargetId(string fileName)
    {
        var normalized = fileName.Trim().ToLowerInvariant();
        return normalized switch
        {
            "rms_branchservice_appsettings.json" or "branch-appsettings.json" or "branch-config.json" or "branch.json" => "branch-config",
            "rms_cashierserver_appsettings.json" or "cashier-server-appsettings.json" or "cashier-server-config.json" or "cashierserver.json" => "cashier-server-config",
            "rms_cashierui_appsettings.json" or "cashier-ui-appsettings.json" or "cashier-ui-config.json" or "cashierui.json" => "cashier-ui-config",
            _ => null,
        };
    }

    private static string? ResolveManifestTargetId(string componentId)
    {
        var normalized = componentId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized is "branch-config" or "cashier-server-config" or "cashier-ui-config") return normalized;
        if (normalized is "branch-database" or "cashier-database") return null;
        if (normalized.Length == 0) throw new RestorePolicyException("restore.archive_manifest_invalid", "The restore archive manifest contains an unsupported component.");
        throw new RestorePolicyException("restore.archive_manifest_invalid", "The restore archive manifest contains an unsupported component.");
    }

    private async Task<ManifestData> ReadManifestAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Length > _limits.MaxManifestBytes)
        {
            throw new RestorePolicyException("restore.archive_manifest_invalid", "The restore archive manifest is too large.");
        }

        byte[] bytes;
        await using (var stream = entry.Open())
        {
            bytes = await ReadBoundedAsync(stream, _limits.MaxManifestBytes, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            var branchCode = root.TryGetProperty("branchCode", out var branchElement) ? branchElement.GetString() : null;
            var schemaVersion = root.TryGetProperty("schemaVersion", out var schemaElement) && schemaElement.TryGetInt32(out var version)
                ? version
                : 0;
            if (string.IsNullOrWhiteSpace(branchCode) || schemaVersion != 1 || !root.TryGetProperty("contents", out var contentsElement) || contentsElement.ValueKind != JsonValueKind.Array)
            {
                throw new RestorePolicyException("restore.archive_manifest_invalid", "The restore archive manifest is incomplete.");
            }

            var contents = new List<ManifestItem>();
            foreach (var item in contentsElement.EnumerateArray())
            {
                if (contents.Count >= _limits.MaxArchiveEntryCount
                    || item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("archiveName", out var nameElement)
                    || !item.TryGetProperty("sizeBytes", out var sizeElement)
                    || !item.TryGetProperty("sha256Checksum", out var checksumElement)
                    || !item.TryGetProperty("componentId", out var componentElement))
                {
                    throw new RestorePolicyException("restore.archive_manifest_invalid", "The restore archive manifest contains invalid content metadata.");
                }

                var name = nameElement.GetString() ?? string.Empty;
                var checksum = checksumElement.GetString() ?? string.Empty;
                var component = componentElement.GetString() ?? string.Empty;
                if (name.Length == 0 || !sizeElement.TryGetInt64(out var size) || size < 0 || !Regex.IsMatch(checksum, "^[A-Fa-f0-9]{64}$"))
                {
                    throw new RestorePolicyException("restore.archive_manifest_invalid", "The restore archive manifest contains invalid checksum metadata.");
                }

                contents.Add(new ManifestItem(name, size, checksum, component));
            }

            if (contents.Select(item => item.ArchiveName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != contents.Count)
            {
                throw new RestorePolicyException("restore.archive_manifest_invalid", "The restore archive manifest contains duplicate entries.");
            }

            return new ManifestData(branchCode.Trim(), schemaVersion, contents);
        }
        catch (RestorePolicyException)
        {
            throw;
        }
        catch
        {
            throw new RestorePolicyException("restore.archive_manifest_invalid", "The restore archive manifest could not be read safely.");
        }
    }

    private static async Task<string> ComputeEntryChecksumAsync(ZipArchiveEntry entry, long expectedLength, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var input = entry.Open();
        var buffer = new byte[CopyBufferSize];
        long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total = checked(total + read);
            if (total > expectedLength) throw new RestorePolicyException("restore.archive_invalid", "The restore archive content exceeds its declared size.");
            hash.AppendData(buffer, 0, read);
        }

        if (total != expectedLength) throw new RestorePolicyException("restore.archive_invalid", "The restore archive content size does not match its metadata.");
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>
    /// Bounds the ZIP central-directory metadata before constructing <see cref="ZipArchive"/>.
    /// The BCL archive object materializes its entry collection during construction, so the
    /// explicit central-directory pass prevents an archive containing millions of tiny entries
    /// from turning metadata inspection into an allocation attack. The second BCL pass is then
    /// limited to the admitted entry count and is used only for content reads.
    /// </summary>
    private async Task ValidateZipCentralDirectoryAsync(
        string path,
        long sourceLength,
        CancellationToken cancellationToken)
    {
        const int endOfCentralDirectoryLength = 22;
        const int maxZipCommentBytes = ushort.MaxValue;
        const int maxEntryNameBytes = 1024;
        const uint endOfCentralDirectorySignature = 0x06054B50;
        const uint centralDirectorySignature = 0x02014B50;

        if (sourceLength < endOfCentralDirectoryLength)
        {
            throw new RestorePolicyException("restore.archive_invalid", "The restore archive does not contain a valid central directory.");
        }

        try
        {
            await using var stream = await _fileSystem.OpenReadAsync(path, cancellationToken).ConfigureAwait(false);
            if (!stream.CanSeek)
            {
                throw new RestorePolicyException("restore.archive_invalid", "The restore archive cannot be inspected as a bounded stream.");
            }

            var tailLength = checked((int)Math.Min(sourceLength, endOfCentralDirectoryLength + maxZipCommentBytes));
            var tail = new byte[tailLength];
            stream.Seek(sourceLength - tailLength, SeekOrigin.Begin);
            await ReadExactlyAsync(stream, tail, cancellationToken).ConfigureAwait(false);

            var endOffset = -1;
            ushort commentLength = 0;
            for (var index = tail.Length - endOfCentralDirectoryLength; index >= 0; index--)
            {
                if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(index, 4)) != endOfCentralDirectorySignature) continue;
                var candidateCommentLength = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(index + 20, 2));
                if (index + endOfCentralDirectoryLength + candidateCommentLength != tail.Length) continue;
                endOffset = index;
                commentLength = candidateCommentLength;
                break;
            }

            if (endOffset < 0)
            {
                throw new RestorePolicyException("restore.archive_invalid", "The restore archive does not contain a valid central directory.");
            }

            var diskNumber = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(endOffset + 4, 2));
            var centralDirectoryDisk = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(endOffset + 6, 2));
            var entriesOnDisk = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(endOffset + 8, 2));
            var totalEntries = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(endOffset + 10, 2));
            var centralDirectorySize = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(endOffset + 12, 4));
            var centralDirectoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(endOffset + 16, 4));

            if (diskNumber != 0 || centralDirectoryDisk != 0 || entriesOnDisk != totalEntries
                || totalEntries == ushort.MaxValue
                || centralDirectorySize == uint.MaxValue
                || centralDirectoryOffset == uint.MaxValue)
            {
                throw new RestorePolicyException("restore.archive_invalid", "The restore archive uses unsupported multi-disk or extended metadata.");
            }

            if (totalEntries > _limits.MaxArchiveEntryCount)
            {
                throw new RestorePolicyException("restore.archive_entry_limit", "The restore archive contains too many entries.");
            }

            var centralEnd = checked((ulong)centralDirectoryOffset + centralDirectorySize);
            var endOfCentralDirectoryAbsolute = checked((ulong)(sourceLength - tailLength + endOffset));
            if (centralEnd != endOfCentralDirectoryAbsolute)
            {
                throw new RestorePolicyException("restore.archive_invalid", "The restore archive central directory is outside the source bounds.");
            }

            stream.Seek(centralDirectoryOffset, SeekOrigin.Begin);
            var fixedHeader = new byte[46];
            long expandedBytes = 0;
            for (var index = 0; index < totalEntries; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ReadExactlyAsync(stream, fixedHeader, cancellationToken).ConfigureAwait(false);
                if (BinaryPrimitives.ReadUInt32LittleEndian(fixedHeader.AsSpan(0, 4)) != centralDirectorySignature)
                {
                    throw new RestorePolicyException("restore.archive_invalid", "The restore archive central directory is malformed.");
                }

                var compressedLength = BinaryPrimitives.ReadUInt32LittleEndian(fixedHeader.AsSpan(20, 4));
                var expandedLength = BinaryPrimitives.ReadUInt32LittleEndian(fixedHeader.AsSpan(24, 4));
                var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(fixedHeader.AsSpan(28, 2));
                var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(fixedHeader.AsSpan(30, 2));
                var entryCommentLength = BinaryPrimitives.ReadUInt16LittleEndian(fixedHeader.AsSpan(32, 2));
                var entryDisk = BinaryPrimitives.ReadUInt16LittleEndian(fixedHeader.AsSpan(34, 2));
                var externalAttributes = BinaryPrimitives.ReadUInt32LittleEndian(fixedHeader.AsSpan(38, 4));
                var localHeaderOffset = BinaryPrimitives.ReadUInt32LittleEndian(fixedHeader.AsSpan(42, 4));

                if (nameLength == 0 || nameLength > maxEntryNameBytes
                    || entryDisk != 0
                    || localHeaderOffset >= sourceLength
                    || compressedLength == uint.MaxValue
                    || expandedLength == uint.MaxValue)
                {
                    throw new RestorePolicyException("restore.archive_invalid", "The restore archive contains unsupported entry metadata.");
                }

                expandedBytes = checked(expandedBytes + expandedLength);
                if (expandedBytes > _limits.MaxExpandedBytes)
                {
                    throw new RestorePolicyException("restore.archive_expanded_size_limit", "The restore archive expands beyond the bounded inspection limit.");
                }

                if (expandedLength > 0
                    && (compressedLength == 0 || (double)expandedLength / compressedLength > _limits.MaxCompressionRatio))
                {
                    throw new RestorePolicyException("restore.archive_compression_ratio", "The restore archive compression characteristics are unsafe.");
                }

                if (((externalAttributes >> 16) & 0xF000) == 0xA000)
                {
                    throw new RestorePolicyException("restore.archive_path_rejected", "The restore archive contains a symbolic-link entry.");
                }

                await SkipExactlyAsync(stream, checked((long)nameLength + extraLength + entryCommentLength), cancellationToken).ConfigureAwait(false);
            }

            if ((ulong)stream.Position != centralEnd)
            {
                throw new RestorePolicyException("restore.archive_invalid", "The restore archive central directory metadata is inconsistent.");
            }
        }
        catch (RestorePolicyException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            throw new RestorePolicyException("restore.archive_invalid", "The restore archive could not be inspected safely.");
        }
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    private static Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken) =>
        ReadExactlyAsync(stream, buffer.AsMemory(), cancellationToken);

    private static async Task SkipExactlyAsync(Stream stream, long bytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[Math.Min(CopyBufferSize, 16 * 1024)];
        var remaining = bytes;
        while (remaining > 0)
        {
            var requested = (int)Math.Min(remaining, buffer.Length);
            await ReadExactlyAsync(stream, buffer.AsMemory(0, requested), cancellationToken).ConfigureAwait(false);
            remaining -= requested;
        }
    }

    private static async Task CopyBoundedAsync(Stream input, Stream output, long expectedLength, CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferSize];
        long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total = checked(total + read);
            if (total > expectedLength) throw new RestorePolicyException("restore.archive_invalid", "The restore archive content exceeds its declared size.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        if (total != expectedLength) throw new RestorePolicyException("restore.archive_invalid", "The restore archive content size does not match its metadata.");
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream input, long maxBytes, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[Math.Min(CopyBufferSize, checked((int)Math.Min(maxBytes, CopyBufferSize)))];
        long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total = checked(total + read);
            if (total > maxBytes) throw new RestorePolicyException("restore.archive_manifest_invalid", "The restore archive manifest is too large.");
            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    private static bool IsWithinDirectory(string root, string target)
    {
        var canonicalRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var canonicalTarget = Path.GetFullPath(target);
        return canonicalTarget.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private async Task DeleteDirectorySafeAsync(string path)
    {
        try { await _fileSystem.DeleteDirectoryAsync(path, CancellationToken.None).ConfigureAwait(false); } catch { }
    }

    private sealed record ArchiveEntryInfo(string Name, ZipArchiveEntry Entry, long Length, long CompressedLength);

    private sealed record ArchiveConfigEntry(
        string TargetId,
        string ArchiveEntryName,
        string StagedSourcePath,
        long SizeBytes,
        string Sha256Checksum);

    private sealed record ArchiveInspection(
        string ArchiveVersion,
        bool HasManifest,
        string StagedBackupPath,
        IReadOnlyList<ArchiveConfigEntry> ConfigEntries,
        IReadOnlyList<string> Warnings,
        IReadOnlyList<ArchiveEntryInfo> Entries)
    {
        public string? StagingDirectory { get; init; }
    }

    private sealed record ManifestData(string BranchCode, int SchemaVersion, IReadOnlyList<ManifestItem> Contents);

    private sealed record ManifestItem(string ArchiveName, long SizeBytes, string Sha256Checksum, string ComponentId);

    private sealed class RestoreExecutionMilestones
    {
        public bool ServicesStopped { get; set; }
        public bool DatabaseRestoreAttempted { get; set; }
        public bool DatabaseRestoreCompleted { get; set; }
        public bool PostDatabaseVerificationCompleted { get; set; }
        public bool ConfigurationOverwriteStarted { get; set; }
        public bool ConfigurationOverwriteCompleted { get; set; }
        public bool ConfigurationRollbackAttempted { get; set; }
        public bool ConfigurationRollbackCompleted { get; set; }
        public bool ServicesRestarted { get; set; }
    }

    private enum ConfigurationApplyStatus
    {
        Completed,
        Failed,
        Cancelled,
        RollbackFailed,
    }

    private sealed record ConfigurationApplyResult(
        ConfigurationApplyStatus Status,
        bool OverwriteStarted,
        bool OverwriteCompleted,
        bool RollbackAttempted,
        bool RollbackCompleted);

    private sealed record RollbackResult(bool Completed);

    private sealed record RollbackFile(string DestinationPath, string BackupPath, bool Existed);
}
