using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using RmsSupportHub.Pos.Agent.Artifacts;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Contracts.V1.Downloader;
using RmsSupportHub.Pos.Contracts.V1.Security;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Runtime;

public static class DownloaderOperation
{
    public const string OperationId = "downloader.batch.trigger";
    public const string HttpMethod = "POST";
    public const string HttpPathTemplate = "/api/v1/downloads/batches";

    public static MutationOperationDescriptor Descriptor { get; } = new(
        OperationId,
        HttpMethod,
        HttpPathTemplate,
        MutationTargetKind.None);
}

public enum DownloaderSecurityFailure
{
    None,
    PrincipalUnavailable,
    MutationTokenInvalid
}

public sealed record DownloaderAgentExecutionResult(
    DownloaderOperationDto? Response,
    DownloaderSecurityFailure SecurityFailure = DownloaderSecurityFailure.None);

/// <summary>
/// Agent composition root for the existing downloader application service. It owns only typed
/// input validation, server configuration projection, mutation authorization, operation retention,
/// and publication of opaque artifact capabilities; SMB and remote HTTP policy remain in the
/// existing Infrastructure adapters.
/// </summary>
public sealed class DownloaderOperationRuntime(
    AgentRuntimeSettingsFactory settingsFactory,
    DbDownloadService downloader,
    ArtifactCatalog artifacts,
    DownloaderOperationStore operations,
    DownloaderIdempotencyStore idempotency,
    AgentOperationConcurrencyGate concurrency,
    IMutationTokenStore mutationTokens,
    IAgentPrincipalSidResolver principalSidResolver)
{
    public async Task<IReadOnlyList<BranchCatalogEntryDto>> GetBranchesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var runtime = await settingsFactory.LoadAsync(cancellationToken).ConfigureAwait(false);
            var known = DownloaderInputPolicy.NormalizeBranchCodes(runtime.Downloader.KnownBranchCodes);
            return known.Select(branch => new BranchCatalogEntryDto(branch, false)).ToArray();
        }
        catch (ArgumentException)
        {
            return [];
        }
    }

    public async Task<DownloaderAgentExecutionResult> ExecuteAsync(
        HttpContext context,
        TriggerBatchRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var correlationId = CorrelationIdContext.TryGet(context) ?? "unavailable";

        if (request is null)
        {
            return NotAttempted(correlationId, DownloaderFailureCodes.InvalidConfiguration, "The downloader request is required.");
        }

        if (!DownloaderIdempotencyStore.IsValidKey(request.IdempotencyKey))
        {
            return NotAttempted(correlationId, "downloader.idempotency_invalid", "A bounded idempotency key is required.");
        }

        IReadOnlyList<string> branches;
        try
        {
            branches = DownloaderInputPolicy.NormalizeBranchCodes(request.BranchCodes);
        }
        catch (ArgumentException)
        {
            return NotAttempted(correlationId, DownloaderFailureCodes.InvalidBranch, "The requested branch selection is invalid.");
        }

        AgentRuntimeSettings runtime;
        try
        {
            runtime = await settingsFactory.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return NotAttempted(correlationId, DownloaderFailureCodes.InvalidConfiguration, "The downloader configuration could not be read.");
        }
        catch
        {
            return NotAttempted(correlationId, DownloaderFailureCodes.InvalidConfiguration, "The downloader configuration could not be read.");
        }

        var settings = runtime.Downloader;
        if (string.IsNullOrWhiteSpace(settings.ApiUrl)
            || string.IsNullOrWhiteSpace(settings.RdbServerIp)
            || string.IsNullOrWhiteSpace(settings.RdbUsername)
            || string.IsNullOrWhiteSpace(settings.BackupRootFolder)
            || string.IsNullOrWhiteSpace(runtime.Configuration.BackupFolder)
            || !Path.IsPathRooted(runtime.Configuration.BackupFolder))
        {
            return NotAttempted(correlationId, DownloaderFailureCodes.InvalidConfiguration, "The downloader configuration is incomplete.");
        }

        if (string.IsNullOrWhiteSpace(settings.RdbPassword))
        {
            return NotAttempted(correlationId, DownloaderFailureCodes.CredentialMissing, "The configured downloader credential is not available.");
        }

        IReadOnlyList<string> knownBranches;
        try
        {
            knownBranches = DownloaderInputPolicy.NormalizeBranchCodes(settings.KnownBranchCodes);
        }
        catch (ArgumentException)
        {
            return NotAttempted(correlationId, DownloaderFailureCodes.NoBranches, "No server-approved downloader branches are configured.");
        }

        if (branches.Any(branch => !knownBranches.Contains(branch, StringComparer.OrdinalIgnoreCase)))
        {
            return NotAttempted(correlationId, DownloaderFailureCodes.InvalidBranch, "One or more requested branches are not server-approved.");
        }

        try
        {
            DownloaderInputPolicy.ValidateSettings(settings);
        }
        catch (ArgumentException)
        {
            return NotAttempted(correlationId, DownloaderFailureCodes.InvalidConfiguration, "The downloader timing or storage configuration is invalid.");
        }

        if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
        {
            return new(null, DownloaderSecurityFailure.PrincipalUnavailable);
        }

        var material = string.Join("|", branches);
        var reservation = idempotency.TryReserve(principalSid, request.IdempotencyKey, material);
        if (reservation.State == AgentIdempotencyReservationState.Completed
            && reservation.OperationId is { } completedId
            && operations.TryGet(principalSid, completedId, out var existing))
        {
            return new(existing);
        }

        if (reservation.State == AgentIdempotencyReservationState.InProgress)
        {
            return NotAttempted(correlationId, "downloader.duplicate_in_progress", "An identical downloader operation is already in progress.");
        }

        if (reservation.State == AgentIdempotencyReservationState.Conflict)
        {
            return NotAttempted(correlationId, "downloader.idempotency_conflict", "The idempotency key is already bound to another branch selection.");
        }

        if (reservation.State == AgentIdempotencyReservationState.Capacity)
        {
            return NotAttempted(correlationId, "downloader.idempotency_capacity", "The Agent cannot retain another downloader operation at this time.");
        }

        var reservationHeld = true;
        AgentOperationLease? lease = null;
        try
        {
            var tokenFailure = TryConsumeMutationToken(context, principalSid);
            if (tokenFailure)
            {
                idempotency.Release(principalSid, request.IdempotencyKey);
                reservationHeld = false;
                return new(null, DownloaderSecurityFailure.MutationTokenInvalid);
            }

            lease = concurrency.TryEnter();
            if (lease is null)
            {
                idempotency.Release(principalSid, request.IdempotencyKey);
                reservationHeld = false;
                return NotAttempted(correlationId, "downloader.operation_busy", "Another downloader operation is already in progress.");
            }

            DownloaderOperationHandle handle;
            try
            {
                handle = operations.Create(principalSid, branches, correlationId);
            }
            catch (DownloaderOperationCapacityException)
            {
                idempotency.Release(principalSid, request.IdempotencyKey);
                reservationHeld = false;
                return NotAttempted(correlationId, "downloader.operation_queue_full", "The Agent cannot retain another downloader operation at this time.");
            }

            idempotency.Bind(principalSid, request.IdempotencyKey, handle.OperationId);
            reservationHeld = false;
            _ = RunAsync(
                principalSid,
                handle.OperationId,
                runtime,
                branches,
                lease,
                cancellationToken: CancellationToken.None);
            lease = null;
            return new(handle.InitialState);
        }
        finally
        {
            if (reservationHeld)
            {
                idempotency.Release(principalSid, request.IdempotencyKey);
            }

            lease?.Dispose();
        }
    }

    public bool TryGet(string principalSid, string operationId, out DownloaderOperationDto? operation) =>
        operations.TryGet(principalSid, operationId, out operation);

    public IAsyncEnumerable<DownloaderOperationDto> Stream(
        string principalSid,
        string operationId,
        CancellationToken cancellationToken = default) =>
        operations.StreamAsync(principalSid, operationId, cancellationToken);

    private async Task RunAsync(
        string principalSid,
        string operationId,
        AgentRuntimeSettings runtime,
        IReadOnlyList<string> branches,
        AgentOperationLease lease,
        CancellationToken cancellationToken)
    {
        try
        {
            operations.Start(operationId);
            var settings = runtime.Downloader;
            var outputRoot = Path.Combine(runtime.Configuration.BackupFolder, "downloader-artifacts", operationId);
            Directory.CreateDirectory(outputRoot);

            RmsSupportHub.Pos.Domain.Models.DownloaderExecutionResult? execution = null;
            var progress = new Progress<string>(detail =>
            {
                var current = execution is null
                    ? null
                    : BuildOutcome(execution.Job.Items, execution.TriggerState, execution.Job.Serial, null);
                operations.Progress(operationId, EstimateProgress(execution?.Job.Items), "downloading", detail, current);
            });
            Action<BranchBackupItem> changed = item =>
            {
                var outcome = execution is null
                    ? null
                    : BuildOutcome(execution.Job.Items, execution.TriggerState, execution.Job.Serial, null);
                operations.Progress(operationId, EstimateProgress(execution?.Job.Items), MapStage(item.Status), SafeDetail(item.ErrorMessage), outcome);
            };

            execution = await downloader.RunWithOutcomeAsync(
                settings,
                branches,
                changed,
                progress,
                cancellationToken).ConfigureAwait(false);

            foreach (var item in execution.Job.Items.Where(item => item.Status == BranchBackupStatus.Ready))
            {
                try
                {
                    await downloader.DownloadAsync(
                        settings,
                        item,
                        outputRoot,
                        new Progress<double>(percent =>
                        {
                            operations.Progress(
                                operationId,
                                EstimateProgress(execution.Job.Items, item, percent),
                                "downloading",
                                "The branch archive is being copied to the Agent artifact store.",
                                BuildOutcome(execution.Job.Items, execution.TriggerState, execution.Job.Serial, null));
                        }),
                        cancellationToken,
                        changed).ConfigureAwait(false);

                    if (item.Status != BranchBackupStatus.Downloaded || string.IsNullOrWhiteSpace(item.LocalDownloadPath))
                    {
                        continue;
                    }

                    var metadata = await PublishArtifactAsync(
                        principalSid,
                        item,
                        cancellationToken).ConfigureAwait(false);
                    item.ArtifactId = metadata.ArtifactId;
                    changed(item);
                }
                catch (ArtifactCatalogCapacityException)
                {
                    item.Status = BranchBackupStatus.Failed;
                    item.FailureCode = DownloaderFailureCodes.ArtifactCatalogFull;
                    item.ErrorMessage = "The Agent artifact retention limit was reached.";
                    changed(item);
                }
                catch
                {
                    item.Status = BranchBackupStatus.Failed;
                    item.FailureCode = DownloaderFailureCodes.ArtifactPublicationFailed;
                    item.ErrorMessage = "The downloaded archive could not be published.";
                    changed(item);
                }
            }

            var finalOutcome = BuildOutcome(
                execution.Job.Items,
                execution.TriggerState,
                execution.Job.Serial,
                execution.TriggerState == DownloaderTriggerState.OutcomeUnknown
                    ? DownloaderOperatorGuidance.TriggerOutcomeUnknown
                    : null);
            var state = execution.TriggerState == DownloaderTriggerState.OutcomeUnknown
                ? DownloaderOperationStateDto.OutcomeUnknown
                : execution.Job.Items.All(item => item.Status == BranchBackupStatus.Downloaded)
                    ? DownloaderOperationStateDto.Completed
                    : DownloaderOperationStateDto.Failed;
            var code = state == DownloaderOperationStateDto.Completed
                ? "downloader.completed"
                : execution.FailureCode
                    ?? execution.Job.Items.Select(item => item.FailureCode).FirstOrDefault(code => !string.IsNullOrWhiteSpace(code))
                    ?? DownloaderFailureCodes.PartialFailure;
            operations.Complete(
                operationId,
                state,
                code,
                state == DownloaderOperationStateDto.OutcomeUnknown
                    ? DownloaderOperatorGuidance.TriggerOutcomeUnknown
                    : state == DownloaderOperationStateDto.Completed
                        ? "The requested branch archives were downloaded and published."
                        : "The downloader operation completed with one or more branch failures.",
                finalOutcome);
        }
        catch (OperationCanceledException)
        {
            var outcome = new DownloaderOperationOutcomeDto(
                branches.Select(branch => new DownloaderBranchOutcomeDto(
                    branch,
                    DownloaderBranchState.Cancelled,
                    0,
                    DownloaderFailureCodes.DownloadCancelled)).ToArray(),
                null,
                DownloaderTriggerStateDto.NotAttempted,
                "The downloader operation was cancelled before completion.");
            operations.Complete(
                operationId,
                DownloaderOperationStateDto.Failed,
                DownloaderFailureCodes.DownloadCancelled,
                "The downloader operation was cancelled before completion.",
                outcome);
        }
        catch
        {
            var outcome = new DownloaderOperationOutcomeDto(
                branches.Select(branch => new DownloaderBranchOutcomeDto(
                    branch,
                    DownloaderBranchState.Failed,
                    0,
                    DownloaderFailureCodes.TriggerOutcomeUnknown)).ToArray(),
                null,
                DownloaderTriggerStateDto.OutcomeUnknown,
                DownloaderOperatorGuidance.TriggerOutcomeUnknown);
            operations.Complete(
                operationId,
                DownloaderOperationStateDto.OutcomeUnknown,
                DownloaderFailureCodes.TriggerOutcomeUnknown,
                DownloaderOperatorGuidance.TriggerOutcomeUnknown,
                outcome);
        }
        finally
        {
            lease.Dispose();
        }
    }

    private async Task<RmsSupportHub.Pos.Contracts.V1.Artifacts.ArtifactMetadataDto> PublishArtifactAsync(
        string principalSid,
        BranchBackupItem item,
        CancellationToken cancellationToken)
    {
        var path = item.LocalDownloadPath!;
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            throw new InvalidOperationException("The downloaded archive is unavailable.");
        }

        await using var stream = File.OpenRead(path);
        var checksum = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        return artifacts.Register(
            principalSid,
            Path.GetFileName(path),
            path,
            fileInfo.Length,
            checksum,
            DateTimeOffset.UtcNow);
    }

    private bool TryConsumeMutationToken(HttpContext context, string principalSid)
    {
        var tokenValues = context.Request.Headers[MutationTokenContract.HeaderName];
        var originValues = context.Request.Headers.Origin;
        if (tokenValues.Count != 1
            || originValues.Count != 1
            || string.IsNullOrWhiteSpace(tokenValues[0])
            || string.IsNullOrWhiteSpace(originValues[0]))
        {
            return true;
        }

        if (!DownloaderOperation.Descriptor.TryResolveHttpPath(null, out var expectedPath)
            || !string.Equals(context.Request.Method, DownloaderOperation.HttpMethod, StringComparison.Ordinal)
            || !string.Equals(context.Request.Path.Value, expectedPath, StringComparison.Ordinal))
        {
            return true;
        }

        return !mutationTokens
            .TryConsume(new MutationTokenValidationRequest(
                tokenValues[0]!,
                principalSid,
                originValues[0]!,
                context.Request.Method,
                DownloaderOperation.OperationId,
                context.Request.Path.Value))
            .Succeeded;
    }

    private static DownloaderAgentExecutionResult NotAttempted(string correlationId, string code, string detail) =>
        new(new DownloaderOperationDto(
            string.Empty,
            DownloaderOperationStateDto.NotAttempted,
            DownloaderOperationStateDto.NotAttempted,
            0,
            "not-attempted",
            detail,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            new([], null, DownloaderTriggerStateDto.NotAttempted),
            code,
            correlationId));

    private static DownloaderOperationOutcomeDto BuildOutcome(
        IReadOnlyList<BranchBackupItem> items,
        DownloaderTriggerState triggerState,
        string? serial,
        string? guidance)
    {
        return new(
            items.Select(item => new DownloaderBranchOutcomeDto(
                item.BranchCode,
                MapState(item.Status),
                ProgressFor(item.Status),
                item.FailureCode,
                item.ArtifactId)).ToArray(),
            serial,
            triggerState switch
            {
                DownloaderTriggerState.Accepted => DownloaderTriggerStateDto.Accepted,
                DownloaderTriggerState.Failed => DownloaderTriggerStateDto.Failed,
                DownloaderTriggerState.OutcomeUnknown => DownloaderTriggerStateDto.OutcomeUnknown,
                _ => DownloaderTriggerStateDto.NotAttempted
            },
            guidance);
    }

    private static DownloaderBranchState MapState(BranchBackupStatus status) => status switch
    {
        BranchBackupStatus.Triggered => DownloaderBranchState.Triggered,
        BranchBackupStatus.Waiting => DownloaderBranchState.Waiting,
        BranchBackupStatus.ZipDetected => DownloaderBranchState.Detected,
        BranchBackupStatus.Validating => DownloaderBranchState.Validating,
        BranchBackupStatus.Ready => DownloaderBranchState.Ready,
        BranchBackupStatus.Downloading => DownloaderBranchState.Downloading,
        BranchBackupStatus.Downloaded => DownloaderBranchState.Completed,
        BranchBackupStatus.TimedOut => DownloaderBranchState.TimedOut,
        BranchBackupStatus.Cancelled => DownloaderBranchState.Cancelled,
        BranchBackupStatus.Failed => DownloaderBranchState.Failed,
        _ => DownloaderBranchState.Pending
    };

    private static int ProgressFor(BranchBackupStatus status) => status switch
    {
        BranchBackupStatus.Triggered => 10,
        BranchBackupStatus.Waiting => 25,
        BranchBackupStatus.ZipDetected => 40,
        BranchBackupStatus.Validating => 55,
        BranchBackupStatus.Ready => 65,
        BranchBackupStatus.Downloading => 80,
        BranchBackupStatus.Downloaded => 100,
        BranchBackupStatus.TimedOut or BranchBackupStatus.Cancelled or BranchBackupStatus.Failed => 100,
        _ => 0
    };

    private static int EstimateProgress(IReadOnlyList<BranchBackupItem>? items, BranchBackupItem? active = null, double activePercent = 0)
    {
        if (items is null || items.Count == 0)
        {
            return 0;
        }

        var total = items.Sum(item =>
        {
            if (ReferenceEquals(item, active) && item.Status == BranchBackupStatus.Downloading)
            {
                return Math.Clamp(activePercent, 0, 100);
            }

            return ProgressFor(item.Status);
        });
        return (int)Math.Round(total / items.Count, MidpointRounding.AwayFromZero);
    }

    private static string MapStage(BranchBackupStatus status) => status switch
    {
        BranchBackupStatus.Triggered => "triggered",
        BranchBackupStatus.Waiting => "waiting",
        BranchBackupStatus.ZipDetected => "detected",
        BranchBackupStatus.Validating => "validating",
        BranchBackupStatus.Ready => "ready",
        BranchBackupStatus.Downloading => "downloading",
        BranchBackupStatus.Downloaded => "completed",
        BranchBackupStatus.TimedOut => "timed-out",
        BranchBackupStatus.Cancelled => "cancelled",
        BranchBackupStatus.Failed => "failed",
        _ => "pending"
    };

    private static string SafeDetail(string? detail) =>
        string.IsNullOrWhiteSpace(detail) ? "The Agent updated the downloader operation state." : detail;
}
