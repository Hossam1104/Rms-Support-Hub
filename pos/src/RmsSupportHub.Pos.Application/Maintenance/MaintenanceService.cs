using System.Security.Cryptography;
using System.Text;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Maintenance;

/// <summary>
/// Server-owned cleanup and branch-reset policy/execution.  The service never accepts a path,
/// branch, database, or table from a browser request; all targets come from service configuration
/// and are recomputed immediately before a destructive adapter call.
/// </summary>
public sealed class MaintenanceService(
    IDatabaseService databaseService,
    IServiceManager serviceManager,
    IMaintenanceFileSystem fileSystem)
{
    private readonly MaintenancePathPolicy _pathPolicy = new(fileSystem);

    // Historical RMS branch-reset scope is code-owned. Configuration may only select/deduplicate
    // these identifiers; it cannot expand the destructive SQL target set.
    private static readonly IReadOnlyDictionary<string, string> ApprovedBranchResetTables =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sales"] = "Sales",
            ["CashierSessions"] = "CashierSessions",
            ["InventoryMovements"] = "InventoryMovements",
        };

    public async Task<CleanupPreviewBuildResult> BuildCleanupPreviewAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        var maintenance = settings.Maintenance ?? new MaintenanceSettings();
        var rejections = new List<MaintenancePolicyRejection>();
        var warnings = new List<string>();
        var configuredServices = settings.Services ?? [];
        var services = ResolveServices(configuredServices, rejections);
        var targets = new List<MaintenanceCleanupTargetPreview>();
        var accepted = new List<MaintenancePathResolution>();
        var rawTargets = maintenance.CleanupTargets is { Count: > 0 }
            ? maintenance.CleanupTargets
            : settings.FoldersToDelete ?? [];

        if (rawTargets.Count == 0)
        {
            rejections.Add(new("cleanup", MaintenanceFailureCodes.InvalidConfiguration, "No cleanup targets are configured."));
        }

        if (configuredServices.Count == 0)
        {
            rejections.Add(new("services", MaintenanceFailureCodes.InvalidConfiguration, "No services are configured for maintenance."));
        }

        for (var index = 0; index < rawTargets.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetId = $"cleanup-{index + 1:D3}";
            var resolution = _pathPolicy.Resolve(targetId, rawTargets[index], maintenance);
            if (!resolution.Accepted || resolution.Resolution is null)
            {
                var code = resolution.RejectionCode ?? MaintenanceFailureCodes.InvalidPath;
                rejections.Add(new(targetId, code, resolution.SafeMessage ?? MaintenanceFailureCodes.PathRejectedMessage));
                targets.Add(new(targetId, false, false, false, null, null, code));
                continue;
            }

            var resolved = resolution.Resolution;
            accepted.Add(resolved);
            targets.Add(new(
                targetId,
                true,
                resolved.Exists,
                resolved.IsDirectory,
                resolved.LengthBytes,
                resolved.ChildCount,
                null));

            if (!resolved.Exists)
            {
                warnings.Add($"{targetId} is already absent; execution will treat it as a no-op.");
            }
        }

        var availableFreeSpace = TryReadFreeSpace(accepted.FirstOrDefault()?.CanonicalPath, warnings);
        if (availableFreeSpace is null)
        {
            warnings.Add("Free-space evidence was not available for the configured cleanup roots.");
        }

        var ready = rejections.Count == 0 && targets.Count > 0;
        MaintenancePreviewIntent? intent = null;
        if (ready)
        {
            var fingerprint = Fingerprint(
                MaintenanceMode.Cleanup,
                settings,
                services,
                accepted.Select(target => $"{target.TargetId}|{target.CanonicalPath}|{target.Exists}|{target.IsDirectory}|{target.LengthBytes}|{target.ChildCount}"),
                string.Empty,
                string.Empty,
                []);
            intent = new(
                MaintenanceMode.Cleanup,
                SafeBranch(settings.BranchCode),
                string.Empty,
                [],
                accepted.Select(target => target.TargetId).ToList(),
                $"CONFIRM CLEANUP {fingerprint[..10].ToUpperInvariant()}",
                fingerprint);
        }

        return new(
            ready,
            ready ? null : rejections.FirstOrDefault()?.Code ?? MaintenanceFailureCodes.PreviewNotReady,
            ready ? null : "The cleanup preview was rejected by server policy.",
            intent,
            targets,
            services,
            rejections,
            warnings,
            availableFreeSpace);
    }

    public async Task<BranchResetPreviewBuildResult> BuildBranchResetPreviewAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        var maintenance = settings.Maintenance ?? new MaintenanceSettings();
        var rejections = new List<MaintenancePolicyRejection>();
        var warnings = new List<string>();
        var configuredServices = settings.Services ?? [];
        var services = ResolveServices(configuredServices, rejections);
        var branchCode = (settings.BranchCode ?? string.Empty).Trim();
        var approvedDatabaseName = DatabaseResolver.ResolveBranchDatabase(settings);
        var databaseName = string.IsNullOrWhiteSpace(maintenance.BranchResetDatabase)
            ? approvedDatabaseName
            : maintenance.BranchResetDatabase.Trim();
        var tableNames = maintenance.BranchResetTables is { Count: > 0 }
            ? maintenance.BranchResetTables.ToArray()
            : ApprovedBranchResetTables.Values.ToArray();

        if (!MaintenancePathPolicy.IsSafeIdentifier(branchCode) || branchCode.Length > 50)
        {
            rejections.Add(new("branch", MaintenanceFailureCodes.BranchInvalid, "The configured branch identity is invalid."));
        }

        if (!MaintenancePathPolicy.IsSafeIdentifier(databaseName))
        {
            rejections.Add(new("database", MaintenanceFailureCodes.DatabaseInvalid, "The configured branch database identity is invalid."));
        }
        else if (!MaintenancePathPolicy.IsSafeIdentifier(approvedDatabaseName)
            || !string.Equals(databaseName, approvedDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            rejections.Add(new("database", MaintenanceFailureCodes.DatabaseOutOfScope, "The configured branch database is outside the server-approved scope."));
        }

        var safeTables = new List<string>();
        foreach (var table in tableNames)
        {
            if (!MaintenancePathPolicy.IsSafeIdentifier(table)
                || !ApprovedBranchResetTables.TryGetValue(table.Trim(), out var approvedTable))
            {
                rejections.Add(new("tables", MaintenanceFailureCodes.DatabaseInvalid, "The configured reset table scope is invalid."));
                continue;
            }

            if (!safeTables.Contains(approvedTable, StringComparer.OrdinalIgnoreCase)) safeTables.Add(approvedTable);
        }

        if (safeTables.Count == 0)
        {
            rejections.Add(new("tables", MaintenanceFailureCodes.DatabaseInvalid, "No branch reset tables are configured."));
        }

        var branchExists = false;
        var maintenanceDatabase = databaseService as IMaintenanceDatabasePreview;
        if (rejections.Count == 0
            && (maintenanceDatabase is null || databaseService is not IMaintenanceDatabaseReset))
        {
            rejections.Add(new("database", MaintenanceFailureCodes.DatabaseScopeUnavailable, "The branch scope could not be verified by the maintenance database adapter."));
        }

        if (rejections.Count == 0)
        {
            try
            {
                branchExists = await maintenanceDatabase!.BranchExistsInDatabaseAsync(
                    settings,
                    databaseName,
                    branchCode,
                    cancellationToken).ConfigureAwait(false);
                if (!branchExists)
                {
                    rejections.Add(new("branch", MaintenanceFailureCodes.BranchNotFound, "The configured branch was not found."));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                rejections.Add(new("database", MaintenanceFailureCodes.DatabaseScopeUnavailable, "The branch scope could not be verified."));
            }
        }

        var tablePreviews = safeTables
            .Select(table => new MaintenanceTablePreview(table, null))
            .ToList();
        if (branchExists && maintenanceDatabase is not null)
        {
            try
            {
                var scope = await maintenanceDatabase.GetBranchResetScopeAsync(
                    settings,
                    databaseName,
                    branchCode,
                    safeTables,
                    cancellationToken).ConfigureAwait(false);
                var byName = scope.ToDictionary(item => item.TableName, StringComparer.OrdinalIgnoreCase);
                tablePreviews = safeTables
                    .Select(table => byName.TryGetValue(table, out var item)
                        ? new MaintenanceTablePreview(table, item.MatchingRows is >= 0 ? item.MatchingRows : null)
                        : new MaintenanceTablePreview(table, null))
                    .ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                warnings.Add("Row-count evidence was unavailable; the configured table scope remains server-owned.");
            }
        }

        if (tablePreviews.Any(item => item.MatchingRows is null))
        {
            warnings.Add("One or more table counts were not safely queryable.");
        }

        var availableFreeSpace = TryReadFreeSpace(
            maintenance.DataRoots?.FirstOrDefault(),
            warnings);

        var ready = rejections.Count == 0;
        MaintenancePreviewIntent? intent = null;
        if (ready)
        {
            var fingerprint = Fingerprint(
                MaintenanceMode.BranchReset,
                settings,
                services,
                [],
                branchCode,
                databaseName,
                safeTables.Select(table => $"{table}|{tablePreviews.First(item => string.Equals(item.TableName, table, StringComparison.OrdinalIgnoreCase)).MatchingRows}"));
            intent = new(
                MaintenanceMode.BranchReset,
                branchCode,
                databaseName,
                safeTables,
                [],
                $"RESET BRANCH {branchCode}",
                fingerprint);
        }

        return new(
            ready,
            ready ? null : rejections.FirstOrDefault()?.Code ?? MaintenanceFailureCodes.PreviewNotReady,
            ready ? null : "The branch reset preview was rejected by server policy.",
            intent,
            tablePreviews,
            services,
            rejections,
            warnings,
            availableFreeSpace);
    }

    public async Task<MaintenanceExecutionResult> ExecuteCleanupAsync(
        AppSettings settings,
        string? expectedFingerprint = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var operation = OperationResult.Running("cleanup_files");
        var items = new List<MaintenanceItemResult>();
        var warnings = new List<string>();
        var recovery = new List<string>();
        var anyAttempt = false;

        var preview = await BuildCleanupPreviewAsync(settings, cancellationToken).ConfigureAwait(false);
        if (!preview.Ready || preview.Intent is null)
        {
            return Finish(operation, items, warnings, recovery, false, OperationStatus.Failed, preview.ErrorCode ?? MaintenanceFailureCodes.PreviewNotReady);
        }

        if (expectedFingerprint is not null && !FixedTimeEquals(expectedFingerprint, preview.Intent.Fingerprint))
        {
            return Finish(operation, items, warnings, recovery, false, OperationStatus.Failed, MaintenanceFailureCodes.PreviewChanged);
        }

        var maintenance = settings.Maintenance ?? new MaintenanceSettings();
        var rawTargets = maintenance.CleanupTargets is { Count: > 0 }
            ? maintenance.CleanupTargets
            : settings.FoldersToDelete ?? [];
        var stopServices = await StopServicesAsync(
            settings.Services ?? [],
            maintenance.StopOnServiceFailure,
            items,
            warnings,
            recovery,
            progress,
            cancellationToken).ConfigureAwait(false);
        anyAttempt |= stopServices.Attempted;
        if (!stopServices.Continue)
        {
            var stopOutcome = ResolveServiceStopOutcome(stopServices, anyAttempt);
            return Finish(
                operation,
                items,
                warnings,
                recovery,
                anyAttempt,
                stopOutcome.Status,
                stopOutcome.FailureCode);
        }

        for (var index = 0; index < rawTargets.Count; index++)
        {
            var targetId = $"cleanup-{index + 1:D3}";
            if (cancellationToken.IsCancellationRequested)
            {
                return Finish(
                    operation,
                    items,
                    warnings,
                    recovery,
                    anyAttempt,
                    anyAttempt ? OperationStatus.PartialSuccess : OperationStatus.Cancelled,
                    anyAttempt ? MaintenanceFailureCodes.RecoveryRequired : null);
            }

            // Re-resolve the exact configured target immediately before crossing the delete seam.
            var resolution = _pathPolicy.Resolve(targetId, rawTargets[index], maintenance);
            if (!resolution.Accepted || resolution.Resolution is null)
            {
                items.Add(new(targetId, "file", "rejected", false, false, false, resolution.RejectionCode, null));
                if (!maintenance.ContinueAfterTargetFailure)
                {
                    return Finish(operation, items, warnings, recovery, anyAttempt, OperationStatus.PartialSuccess, resolution.RejectionCode);
                }

                continue;
            }

            var target = resolution.Resolution;
            if (!target.Exists)
            {
                items.Add(new(targetId, "file", "already_absent", false, false, false, null, null));
                continue;
            }

            progress?.Report($"Deleting {targetId}...");
            if (cancellationToken.IsCancellationRequested)
            {
                return Finish(
                    operation,
                    items,
                    warnings,
                    recovery,
                    anyAttempt,
                    anyAttempt ? OperationStatus.PartialSuccess : OperationStatus.Cancelled,
                    anyAttempt ? MaintenanceFailureCodes.RecoveryRequired : null);
            }

            var attempted = true;
            anyAttempt = true;
            try
            {
                await fileSystem.DeleteAsync(target.CanonicalPath, target.IsDirectory, cancellationToken).ConfigureAwait(false);
                items.Add(new(targetId, "file", "completed", attempted, true, false, null, null));
            }
            catch (OperationCanceledException)
            {
                var guidance = MaintenanceFailureCodes.RecoveryGuidance;
                recovery.Add(guidance);
                items.Add(new(targetId, "file", "recovery_required", attempted, false, true, MaintenanceFailureCodes.TargetDeleteInterrupted, guidance));
                return Finish(operation, items, warnings, recovery, anyAttempt, OperationStatus.PartialSuccess, MaintenanceFailureCodes.TargetDeleteInterrupted);
            }
            catch
            {
                var guidance = MaintenanceFailureCodes.RecoveryGuidance;
                recovery.Add(guidance);
                items.Add(new(targetId, "file", "recovery_required", attempted, false, true, MaintenanceFailureCodes.TargetDeleteFailed, guidance));
                if (!maintenance.ContinueAfterTargetFailure)
                {
                    return Finish(operation, items, warnings, recovery, anyAttempt, OperationStatus.PartialSuccess, MaintenanceFailureCodes.TargetDeleteFailed);
                }
            }
        }

        return Finish(
            operation,
            items,
            warnings,
            recovery,
            anyAttempt,
            items.Any(item => item.FailureCode is not null)
                ? anyAttempt ? OperationStatus.PartialSuccess : OperationStatus.Failed
                : OperationStatus.Success,
            items.FirstOrDefault(item => item.FailureCode is not null)?.FailureCode);
    }

    public async Task<MaintenanceExecutionResult> ExecuteBranchResetAsync(
        AppSettings settings,
        string? expectedFingerprint = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var operation = OperationResult.Running("reset_branch_data");
        var items = new List<MaintenanceItemResult>();
        var warnings = new List<string>();
        var recovery = new List<string>();
        var anyAttempt = false;

        var preview = await BuildBranchResetPreviewAsync(settings, cancellationToken).ConfigureAwait(false);
        if (!preview.Ready || preview.Intent is null)
        {
            return Finish(operation, items, warnings, recovery, false, OperationStatus.Failed, preview.ErrorCode ?? MaintenanceFailureCodes.PreviewNotReady);
        }

        if (expectedFingerprint is not null && !FixedTimeEquals(expectedFingerprint, preview.Intent.Fingerprint))
        {
            return Finish(operation, items, warnings, recovery, false, OperationStatus.Failed, MaintenanceFailureCodes.PreviewChanged);
        }

        var maintenance = settings.Maintenance ?? new MaintenanceSettings();
        var stopServices = await StopServicesAsync(
            settings.Services ?? [],
            maintenance.StopOnServiceFailure,
            items,
            warnings,
            recovery,
            progress,
            cancellationToken).ConfigureAwait(false);
        anyAttempt |= stopServices.Attempted;
        if (!stopServices.Continue)
        {
            var stopOutcome = ResolveServiceStopOutcome(stopServices, anyAttempt);
            return Finish(operation, items, warnings, recovery, anyAttempt, stopOutcome.Status, stopOutcome.FailureCode);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Finish(operation, items, warnings, recovery, anyAttempt, anyAttempt ? OperationStatus.PartialSuccess : OperationStatus.Cancelled, anyAttempt ? MaintenanceFailureCodes.RecoveryRequired : null);
        }

        var database = preview.Intent.DatabaseName;
        var branch = preview.Intent.BranchCode;
        var tables = preview.Intent.TableNames;
        progress?.Report("Resetting the server-resolved branch scope...");
        if (cancellationToken.IsCancellationRequested)
        {
            return Finish(
                operation,
                items,
                warnings,
                recovery,
                anyAttempt,
                anyAttempt ? OperationStatus.PartialSuccess : OperationStatus.Cancelled,
                anyAttempt ? MaintenanceFailureCodes.RecoveryRequired : null);
        }

        if (databaseService is not IMaintenanceDatabaseReset reset)
        {
            return Finish(
                operation,
                items,
                warnings,
                recovery,
                anyAttempt,
                OperationStatus.Failed,
                MaintenanceFailureCodes.DatabaseScopeUnavailable);
        }

        anyAttempt = true; // The flag is set immediately before the injected destructive SQL seam.
        try
        {
            await reset.ResetBranchDataAsync(settings, database, branch, tables, cancellationToken).ConfigureAwait(false);

            items.Add(new("branch-reset-sql", "database", "completed", true, true, false, null, null));
        }
        catch (OperationCanceledException)
        {
            var guidance = MaintenanceFailureCodes.DatabaseRecoveryGuidance;
            recovery.Add(guidance);
            items.Add(new("branch-reset-sql", "database", "recovery_required", true, false, true, MaintenanceFailureCodes.SqlResetInterrupted, guidance));
            return Finish(operation, items, warnings, recovery, anyAttempt, OperationStatus.PartialSuccess, MaintenanceFailureCodes.SqlResetInterrupted);
        }
        catch
        {
            var guidance = MaintenanceFailureCodes.DatabaseRecoveryGuidance;
            recovery.Add(guidance);
            items.Add(new("branch-reset-sql", "database", "recovery_required", true, false, true, MaintenanceFailureCodes.SqlResetFailed, guidance));
            return Finish(operation, items, warnings, recovery, anyAttempt, OperationStatus.PartialSuccess, MaintenanceFailureCodes.SqlResetFailed);
        }

        return Finish(operation, items, warnings, recovery, anyAttempt, OperationStatus.Success, null);
    }

    private async Task<ServiceStopResult> StopServicesAsync(
        IReadOnlyList<string> configuredServices,
        bool stopOnFailure,
        List<MaintenanceItemResult> items,
        List<string> warnings,
        List<string> recovery,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var attempted = false;
        foreach (var rawService in configuredServices)
        {
            var serviceId = MaintenancePathPolicy.SafeLogicalValue(rawService, "service");
            if (!MaintenancePathPolicy.IsSafeIdentifier(rawService))
            {
                items.Add(new(serviceId, "service", "rejected", false, false, false, MaintenanceFailureCodes.ServiceInvalid, null));
                if (stopOnFailure) return new(attempted, false, false, MaintenanceFailureCodes.ServiceInvalid);
                continue;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return new(attempted, false, !attempted, attempted ? MaintenanceFailureCodes.RecoveryRequired : null);
            }

            progress?.Report($"Stopping {serviceId}...");
            if (cancellationToken.IsCancellationRequested)
            {
                return new(attempted, false, true, attempted ? MaintenanceFailureCodes.RecoveryRequired : null);
            }

            attempted = true;
            try
            {
                await serviceManager.ControlAsync(rawService.Trim(), ServiceControlAction.Stop, cancellationToken).ConfigureAwait(false);
                items.Add(new(serviceId, "service", "completed", true, true, false, null, null));
            }
            catch (OperationCanceledException)
            {
                var guidance = MaintenanceFailureCodes.RecoveryGuidance;
                recovery.Add(guidance);
                items.Add(new(serviceId, "service", "recovery_required", true, false, true, MaintenanceFailureCodes.ServiceStopInterrupted, guidance));
                return new(attempted, false, true, MaintenanceFailureCodes.ServiceStopInterrupted);
            }
            catch
            {
                var guidance = MaintenanceFailureCodes.RecoveryGuidance;
                recovery.Add(guidance);
                items.Add(new(serviceId, "service", "recovery_required", true, false, true, MaintenanceFailureCodes.ServiceStopFailed, guidance));
                warnings.Add($"{serviceId} could not be stopped; no exception details are retained.");
                if (stopOnFailure) return new(attempted, false, false, MaintenanceFailureCodes.ServiceStopFailed);
            }
        }

        return new(attempted, true, false, null);
    }

    private static ServiceStopOutcome ResolveServiceStopOutcome(
        ServiceStopResult stopServices,
        bool destructiveAttempted)
    {
        if (stopServices.Cancelled)
        {
            return destructiveAttempted
                ? new(OperationStatus.PartialSuccess, stopServices.FailureCode ?? MaintenanceFailureCodes.RecoveryRequired)
                : new(OperationStatus.Cancelled, null);
        }

        return destructiveAttempted
            ? new(OperationStatus.PartialSuccess, stopServices.FailureCode ?? MaintenanceFailureCodes.ServiceStopFailed)
            : new(OperationStatus.Failed, stopServices.FailureCode ?? MaintenanceFailureCodes.ServiceStopFailed);
    }

    private static IReadOnlyList<string> ResolveServices(
        IReadOnlyList<string>? configured,
        List<MaintenancePolicyRejection> rejections)
    {
        var services = new List<string>();
        foreach (var service in configured ?? [])
        {
            if (!MaintenancePathPolicy.IsSafeIdentifier(service))
            {
                rejections.Add(new("services", MaintenanceFailureCodes.ServiceInvalid, "A configured service identity is invalid."));
                continue;
            }

            var safe = service.Trim();
            if (!services.Contains(safe, StringComparer.OrdinalIgnoreCase)) services.Add(safe);
        }

        return services;
    }

    private long? TryReadFreeSpace(string? path, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { return fileSystem.TryGetAvailableFreeSpace(path); }
        catch { return null; }
    }

    private static MaintenanceExecutionResult Finish(
        OperationResult operation,
        IReadOnlyList<MaintenanceItemResult> items,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> recovery,
        bool destructiveAttempted,
        OperationStatus status,
        string? failureCode)
    {
        var normalizedCode = status switch
        {
            OperationStatus.PartialSuccess => failureCode ?? MaintenanceFailureCodes.PartialFailure,
            OperationStatus.Failed => failureCode ?? MaintenanceFailureCodes.OperationFailed,
            OperationStatus.Cancelled when destructiveAttempted => failureCode ?? MaintenanceFailureCodes.RecoveryRequired,
            _ => null,
        };

        if (normalizedCode is not null) operation.AddError(normalizedCode);
        if (status == OperationStatus.Cancelled)
        {
            operation.AddMessage(destructiveAttempted
                ? "Maintenance cancellation left an outcome that requires verification."
                : "Maintenance was cancelled before destructive work began.");
        }

        operation.Finalize(status);
        var evidence = new MaintenanceExecutionEvidence(
            destructiveAttempted,
            recovery.Count > 0 || items.Any(item => item.ResidueUncertain),
            items.ToList(),
            warnings.ToList(),
            recovery.ToList());
        return new(operation, evidence, normalizedCode);
    }

    private static string Fingerprint(
        MaintenanceMode mode,
        AppSettings settings,
        IReadOnlyList<string> services,
        IEnumerable<string> resolvedTargets,
        string branchCode,
        string databaseName,
        IEnumerable<string> tables)
    {
        var maintenance = settings.Maintenance ?? new MaintenanceSettings();
        var material = string.Join(
            "\n",
            mode,
            SafeBranch(settings.BranchCode),
            branchCode,
            databaseName,
            string.Join("|", services),
            string.Join("|", tables),
            string.Join("|", resolvedTargets),
            string.Join("|", maintenance.ManagedRoots ?? []),
            string.Join("|", maintenance.DataRoots ?? []),
            string.Join("|", maintenance.InstallRoots ?? []),
            string.Join("|", maintenance.ProtectedRoots ?? []),
            maintenance.AllowUncPaths,
            maintenance.RejectReparsePoints,
            maintenance.StopOnServiceFailure,
            maintenance.ContinueAfterTargetFailure);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    private static string SafeBranch(string? branch) =>
        MaintenancePathPolicy.SafeLogicalValue(branch, "unconfigured-branch");

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected ?? string.Empty);
        var actualBytes = Encoding.UTF8.GetBytes(actual ?? string.Empty);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private sealed record ServiceStopResult(
        bool Attempted,
        bool Continue,
        bool Cancelled,
        string? FailureCode);

    private sealed record ServiceStopOutcome(
        OperationStatus Status,
        string? FailureCode);
}
