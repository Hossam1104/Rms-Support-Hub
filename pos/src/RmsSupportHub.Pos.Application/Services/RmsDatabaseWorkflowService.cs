using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Exceptions;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Services;

/// <summary>
/// Application policy for the Agent-owned RMS database backup and restore slice. It accepts only a
/// canonical <see cref="RmsDatabaseKind"/> and delegates all secret-bearing SQL and filesystem
/// work to injected ports. Browser transport, mutation tokens, operation state, and audit are kept
/// in the Agent composition layer.
/// </summary>
public sealed class RmsDatabaseWorkflowService(
    IRmsDatabaseDiagnostics diagnostics,
    IRmsDatabaseSqlOperations sql,
    IRmsDatabaseBackupStorage storage,
    IServiceManager serviceManager,
    RmsDatabaseStorageOptions storageOptions,
    TimeProvider clock) : IRmsDatabaseWorkflow
{
    public async Task<RmsDatabaseWorkflowResult> BackupAsync(
        RmsDatabaseKind database,
        IProgress<RmsDatabaseProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var definition = RmsDatabaseCatalog.For(database);
        Report(progress, 5, "preflight", "Checking the installed RMS database configuration.");

        var preflight = await CheckDatabaseAsync(database, cancellationToken).ConfigureAwait(false);
        if (preflight is not null)
        {
            Report(progress, 100, "not-attempted", preflight.Detail);
            return preflight;
        }

        RmsDatabaseBackupAllocation allocation;
        try
        {
            allocation = await storage.AllocateAsync(database, clock.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return NotAttempted(
                "backup_destination_unavailable",
                "The Agent-owned backup destination is unavailable.");
        }

        Report(progress, 20, "dispatch", "Dispatching the typed SQL Server backup.");
        var result = await sql.BackupAsync(database, allocation.ServerPath, cancellationToken)
            .ConfigureAwait(false);
        if (result.Outcome != RmsDatabaseSqlOutcome.Completed)
        {
            Report(progress, 100, StageFor(result.Outcome), result.Detail);
            return new(
                MapOutcome(result.Outcome),
                result.Code,
                result.Detail,
                null,
                result.Outcome == RmsDatabaseSqlOutcome.OutcomeUnknown,
                result.Outcome == RmsDatabaseSqlOutcome.OutcomeUnknown,
                []);
        }

        Report(progress, 80, "catalog", "Registering the server-owned backup artifact.");
        var artifact = await storage.RegisterAsync(database, allocation, cancellationToken)
            .ConfigureAwait(false);
        if (artifact is null)
        {
            Report(progress, 100, "outcome-unknown", "The SQL backup completed, but its approved artifact could not be registered.");
            return new(
                RmsDatabaseWorkflowOutcome.OutcomeUnknown,
                "backup_artifact_unavailable",
                "The SQL backup completed, but the approved backup artifact is unavailable. Do not retry automatically; inspect the Agent-owned backup catalog.",
                null,
                true,
                true,
                []);
        }

        Report(progress, 100, "completed", "The RMS database backup completed.");
        return new(
            RmsDatabaseWorkflowOutcome.Completed,
            "backup_completed",
            $"The {definition.DisplayName.ToLowerInvariant()} backup completed.",
            artifact,
            false,
            false,
            []);
    }

    public async Task<RmsDatabaseWorkflowResult> RestoreAsync(
        RmsDatabaseKind database,
        string artifactId,
        IProgress<RmsDatabaseProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var definition = RmsDatabaseCatalog.For(database);
        Report(progress, 3, "preflight", "Checking the approved backup artifact.");

        var artifact = await storage.ResolveAsync(database, artifactId, cancellationToken).ConfigureAwait(false);
        if (artifact is null)
        {
            return NotAttempted(
                "restore_backup_not_approved",
                "The selected backup is not an approved Agent-owned artifact.");
        }

        var preflight = await CheckDatabaseAsync(database, cancellationToken).ConfigureAwait(false);
        if (preflight is not null)
        {
            Report(progress, 100, "not-attempted", preflight.Detail);
            return preflight;
        }

        Report(progress, 15, "inspect", "Validating the approved SQL backup before service coordination.");
        var inspection = await sql.InspectBackupAsync(database, artifact.ServerPath, cancellationToken)
            .ConfigureAwait(false);
        if (inspection.Outcome != RmsDatabaseSqlOutcome.Completed)
        {
            Report(progress, 100, StageFor(inspection.Outcome), inspection.Detail);
            return new(
                MapOutcome(inspection.Outcome),
                inspection.Code,
                inspection.Detail,
                null,
                false,
                inspection.Outcome == RmsDatabaseSqlOutcome.OutcomeUnknown,
                []);
        }

        if (inspection.LogicalFiles.Count == 0)
        {
            return NotAttempted(
                "restore_sql_file_list_invalid",
                "The approved backup did not provide a usable SQL file list.");
        }

        var serviceState = await serviceManager.GetStatusAsync(definition.ServiceName, cancellationToken)
            .ConfigureAwait(false);
        if (serviceState is ServiceStatus.Unknown or ServiceStatus.Transitioning or ServiceStatus.Paused)
        {
            return NotAttempted(
                "restore_service_state_unavailable",
                "The corresponding RMS service state could not be established; the restore was not attempted.");
        }

        var serviceWasRunning = serviceState == ServiceStatus.Running;
        var serviceStopped = false;
        var warnings = new List<string>();
        var destructiveAttempted = false;
        var recoveryRequired = false;
        RmsDatabaseWorkflowResult? workflowResult = null;

        try
        {
            if (serviceWasRunning)
            {
                Report(progress, 25, "service-stop", "Stopping only the RMS service associated with this database.");
                try
                {
                    await serviceManager.ControlAsync(
                        definition.ServiceName,
                        ServiceControlAction.Stop,
                        cancellationToken).ConfigureAwait(false);
                    serviceStopped = true;
                }
                catch (ServiceControlRejectedException)
                {
                    return NotAttempted(
                        "restore_service_stop_rejected",
                        "The corresponding RMS service rejected the stop request; the restore was not attempted.");
                }
                catch
                {
                    return new(
                        RmsDatabaseWorkflowOutcome.OutcomeUnknown,
                        "restore_service_stop_unknown",
                        "The corresponding RMS service stop outcome is unknown; the restore was not attempted.",
                        null,
                        false,
                        true,
                        []);
                }
            }
            else if (serviceState == ServiceStatus.NotFound)
            {
                warnings.Add("The corresponding RMS Windows service was not found; no service stop was required.");
            }

            Report(progress, 35, "dispatch", "Dispatching the typed SQL Server restore.");
            var restore = await sql.RestoreAsync(
                    database,
                    artifact.ServerPath,
                    inspection.LogicalFiles,
                    storageOptions.DatabaseFilesRootPath,
                    cancellationToken)
                .ConfigureAwait(false);
            destructiveAttempted = restore.Outcome is RmsDatabaseSqlOutcome.Completed or RmsDatabaseSqlOutcome.OutcomeUnknown;
            recoveryRequired = restore.RecoveryRequired;
            warnings.AddRange(restore.Warnings);

            if (restore.Outcome == RmsDatabaseSqlOutcome.Completed)
            {
                Report(progress, 78, "verify", "Verifying the restored database identity.");
                var verified = await sql.VerifyDatabaseAsync(database, cancellationToken).ConfigureAwait(false);
                if (!verified)
                {
                    workflowResult = new(
                        RmsDatabaseWorkflowOutcome.OutcomeUnknown,
                        "restore_verification_failed",
                        "The restore dispatch completed, but the restored database could not be verified. Check database state before any retry.",
                        null,
                        true,
                        true,
                        warnings);
                }
                else
                {
                    workflowResult = new(
                        RmsDatabaseWorkflowOutcome.Completed,
                        "restore_completed",
                        $"The {definition.DisplayName.ToLowerInvariant()} restore completed.",
                        artifact,
                        true,
                        recoveryRequired,
                        warnings);
                }
            }
            else
            {
                workflowResult = new(
                    MapOutcome(restore.Outcome),
                    restore.Code,
                    restore.Detail,
                    null,
                    destructiveAttempted,
                    recoveryRequired || restore.Outcome == RmsDatabaseSqlOutcome.OutcomeUnknown,
                    warnings);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            workflowResult = new(
                RmsDatabaseWorkflowOutcome.OutcomeUnknown,
                "restore_cancelled_after_dispatch",
                "The restore operation was interrupted after service/database work began. Check database and service state before any retry.",
                null,
                destructiveAttempted,
                true,
                warnings);
        }
        finally
        {
            if (serviceStopped && serviceWasRunning)
            {
                Report(progress, 90, "service-recovery", "Restoring the RMS service to its original running state.");
                try
                {
                    await serviceManager.ControlAsync(
                        definition.ServiceName,
                        ServiceControlAction.Start,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    recoveryRequired = true;
                    warnings.Add("The RMS service could not be returned to its original running state.");
                }
            }
        }

        if (workflowResult is null)
        {
            return new(
                RmsDatabaseWorkflowOutcome.OutcomeUnknown,
                "restore_outcome_unknown",
                "The restore outcome is unknown. Check database and service state before any retry.",
                null,
                destructiveAttempted,
                true,
                warnings);
        }

        if (recoveryRequired && !workflowResult.RecoveryRequired)
        {
            workflowResult = workflowResult with
            {
                RecoveryRequired = true,
                Warnings = warnings
            };
        }
        else if (!ReferenceEquals(workflowResult.Warnings, warnings))
        {
            workflowResult = workflowResult with { Warnings = warnings };
        }

        Report(progress, 100, StageFor(workflowResult.Outcome), workflowResult.Detail);
        return workflowResult;
    }

    private async Task<RmsDatabaseWorkflowResult?> CheckDatabaseAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken)
    {
        var result = await diagnostics.DiagnoseAsync(database, cancellationToken).ConfigureAwait(false);
        return result.Status switch
        {
            RmsDatabaseDiagnosticStatus.Reachable when result.DatabaseNameMatches == true => null,
            RmsDatabaseDiagnosticStatus.DatabaseNameMismatch => NotAttempted(
                "database_name_mismatch",
                "The installed RMS database identity does not match the canonical target; no database operation was attempted."),
            RmsDatabaseDiagnosticStatus.AuthenticationFailed => NotAttempted(
                "database_authentication_failed",
                "The installed RMS database authentication failed; no database operation was attempted."),
            RmsDatabaseDiagnosticStatus.DatabaseUnavailable => NotAttempted(
                "database_unavailable",
                "The installed RMS database is unavailable; no database operation was attempted."),
            RmsDatabaseDiagnosticStatus.NotConfigured or RmsDatabaseDiagnosticStatus.ConfigurationInvalid => NotAttempted(
                "database_configuration_invalid",
                "The installed RMS database configuration is unavailable or invalid; no database operation was attempted."),
            _ => NotAttempted(
                "database_unreachable",
                "The installed RMS database could not be reached; no database operation was attempted.")
        };
    }

    private static RmsDatabaseWorkflowResult NotAttempted(string code, string detail) =>
        new(RmsDatabaseWorkflowOutcome.NotAttempted, code, detail, null, false, false, []);

    private static RmsDatabaseWorkflowOutcome MapOutcome(RmsDatabaseSqlOutcome outcome) => outcome switch
    {
        RmsDatabaseSqlOutcome.Completed => RmsDatabaseWorkflowOutcome.Completed,
        RmsDatabaseSqlOutcome.OutcomeUnknown => RmsDatabaseWorkflowOutcome.OutcomeUnknown,
        RmsDatabaseSqlOutcome.Failed => RmsDatabaseWorkflowOutcome.Failed,
        _ => RmsDatabaseWorkflowOutcome.NotAttempted
    };

    private static string StageFor(RmsDatabaseSqlOutcome outcome) => outcome switch
    {
        RmsDatabaseSqlOutcome.Completed => "completed",
        RmsDatabaseSqlOutcome.OutcomeUnknown => "outcome-unknown",
        RmsDatabaseSqlOutcome.Failed => "failed",
        _ => "not-attempted"
    };

    private static string StageFor(RmsDatabaseWorkflowOutcome outcome) => outcome switch
    {
        RmsDatabaseWorkflowOutcome.Completed => "completed",
        RmsDatabaseWorkflowOutcome.OutcomeUnknown => "outcome-unknown",
        RmsDatabaseWorkflowOutcome.Failed => "failed",
        _ => "not-attempted"
    };

    private static void Report(
        IProgress<RmsDatabaseProgress>? progress,
        int percent,
        string stage,
        string detail) =>
        progress?.Report(new(Math.Clamp(percent, 0, 100), stage, detail));
}
