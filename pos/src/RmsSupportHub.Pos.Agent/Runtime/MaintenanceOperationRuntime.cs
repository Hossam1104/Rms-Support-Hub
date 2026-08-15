using Microsoft.AspNetCore.Http;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Application.Maintenance;
using RmsSupportHub.Pos.Contracts.V1.Maintenance;
using RmsSupportHub.Pos.Contracts.V1.Security;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Runtime;

public static class MaintenanceOperation
{
    public const string CleanupOperationId = "maintenance.cleanup.execute";
    public const string CleanupHttpPath = "/api/v1/maintenance/cleanup/execute";
    public const string BranchResetOperationId = "maintenance.branch-reset.execute";
    public const string BranchResetHttpPath = "/api/v1/maintenance/reset/execute";

    public static MutationOperationDescriptor CleanupDescriptor { get; } = new(
        CleanupOperationId,
        "POST",
        CleanupHttpPath,
        MutationTargetKind.None);

    public static MutationOperationDescriptor BranchResetDescriptor { get; } = new(
        BranchResetOperationId,
        "POST",
        BranchResetHttpPath,
        MutationTargetKind.None);
}

public enum MaintenanceSecurityFailure
{
    None,
    PrincipalUnavailable,
    MutationTokenInvalid
}

public sealed record MaintenancePreviewResult<T>(
    T? Response,
    MaintenanceSecurityFailure SecurityFailure = MaintenanceSecurityFailure.None)
    where T : class;

public sealed record MaintenanceAgentExecutionResult(
    MaintenanceOperationDto? Response,
    MaintenanceSecurityFailure SecurityFailure = MaintenanceSecurityFailure.None);

/// <summary>
/// Agent composition root for server-owned cleanup and branch reset. Preview is read-only but its
/// challenge is principal-bound; execute requires the challenge, exact confirmation, a one-use
/// method/path-bound mutation token, bounded idempotency, and the existing MaintenanceService
/// policy re-evaluation immediately before the adapter seams.
/// </summary>
public sealed class MaintenanceOperationRuntime(
    AgentRuntimeSettingsFactory settingsFactory,
    MaintenanceService maintenance,
    MaintenanceChallengeStore challenges,
    MaintenanceOperationStore operations,
    MaintenanceIdempotencyStore idempotency,
    AgentOperationConcurrencyGate concurrency,
    IMutationTokenStore mutationTokens,
    IAgentPrincipalSidResolver principalSidResolver,
    IAgentAuditSink audit)
{
    public async Task<MaintenancePreviewResult<CleanupPreviewDto>> PreviewCleanupAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
        {
            return new(null, MaintenanceSecurityFailure.PrincipalUnavailable);
        }

        var correlationId = CorrelationIdContext.TryGet(context) ?? "unavailable";
        try
        {
            var runtime = await settingsFactory.LoadAsync(cancellationToken).ConfigureAwait(false);
            var preview = await maintenance.BuildCleanupPreviewAsync(runtime.Settings, cancellationToken).ConfigureAwait(false);
            MaintenanceChallenge? challenge = null;
            if (preview.Ready && preview.Intent is not null)
            {
                try
                {
                    challenge = challenges.Issue(principalSid, preview.Intent);
                }
                catch (MaintenanceChallengeCapacityException)
                {
                    preview = preview with
                    {
                        Ready = false,
                        ErrorCode = "maintenance.challenge_capacity",
                        SafeMessage = "The Agent cannot retain another maintenance preview.",
                        Intent = null,
                        Rejections = [.. preview.Rejections, new("challenge", "maintenance.challenge_capacity", "The Agent cannot retain another maintenance preview.")]
                    };
                }
            }

            return new(ToCleanupDto(preview, challenge), MaintenanceSecurityFailure.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(ToCleanupError(correlationId, "maintenance.preview_cancelled", "The cleanup preview was not completed."));
        }
        catch
        {
            return new(ToCleanupError(correlationId, MaintenanceFailureCodes.InvalidConfiguration, "The cleanup preview could not be built."));
        }
    }

    public async Task<MaintenancePreviewResult<BranchResetPreviewDto>> PreviewBranchResetAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
        {
            return new(null, MaintenanceSecurityFailure.PrincipalUnavailable);
        }

        var correlationId = CorrelationIdContext.TryGet(context) ?? "unavailable";
        try
        {
            var runtime = await settingsFactory.LoadAsync(cancellationToken).ConfigureAwait(false);
            var preview = await maintenance.BuildBranchResetPreviewAsync(runtime.Settings, cancellationToken).ConfigureAwait(false);
            MaintenanceChallenge? challenge = null;
            if (preview.Ready && preview.Intent is not null)
            {
                try
                {
                    challenge = challenges.Issue(principalSid, preview.Intent);
                }
                catch (MaintenanceChallengeCapacityException)
                {
                    preview = preview with
                    {
                        Ready = false,
                        ErrorCode = "maintenance.challenge_capacity",
                        SafeMessage = "The Agent cannot retain another maintenance preview.",
                        Intent = null,
                        Rejections = [.. preview.Rejections, new("challenge", "maintenance.challenge_capacity", "The Agent cannot retain another maintenance preview.")]
                    };
                }
            }

            return new(ToBranchResetDto(preview, challenge), MaintenanceSecurityFailure.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(ToBranchResetError(correlationId, "maintenance.preview_cancelled", "The branch reset preview was not completed."));
        }
        catch
        {
            return new(ToBranchResetError(correlationId, MaintenanceFailureCodes.InvalidConfiguration, "The branch reset preview could not be built."));
        }
    }

    public Task<MaintenanceAgentExecutionResult> ExecuteCleanupAsync(
        HttpContext context,
        CleanupExecuteRequestDto? request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(context, request?.ChallengeId, request?.TypedConfirmation, request?.IdempotencyKey, "cleanup", cancellationToken);

    public Task<MaintenanceAgentExecutionResult> ExecuteBranchResetAsync(
        HttpContext context,
        BranchResetExecuteRequestDto? request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(context, request?.ChallengeId, request?.TypedConfirmation, request?.IdempotencyKey, "branch-reset", cancellationToken);

    public bool TryGet(string principalSid, string operationId, out MaintenanceOperationDto? operation) =>
        operations.TryGet(principalSid, operationId, out operation);

    public IAsyncEnumerable<MaintenanceOperationDto> Stream(
        string principalSid,
        string operationId,
        CancellationToken cancellationToken = default) =>
        operations.StreamAsync(principalSid, operationId, cancellationToken);

    private async Task<MaintenanceAgentExecutionResult> ExecuteAsync(
        HttpContext context,
        string? challengeId,
        string? typedConfirmation,
        string? idempotencyKey,
        string mode,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIdContext.TryGet(context) ?? "unavailable";
        if (string.IsNullOrWhiteSpace(challengeId)
            || string.IsNullOrWhiteSpace(typedConfirmation)
            || !MaintenanceIdempotencyStore.IsValidKey(idempotencyKey))
        {
            return NotAttempted(mode, correlationId, "maintenance.invalid_request", "A bounded challenge, confirmation, and idempotency key are required.");
        }

        if (!principalSidResolver.TryGetSid(context.User, out var principalSid))
        {
            return new(null, MaintenanceSecurityFailure.PrincipalUnavailable);
        }

        var reservation = idempotency.TryReserve(
            principalSid,
            mode,
            idempotencyKey!,
            challengeId!);
        if (reservation.State == AgentIdempotencyReservationState.Completed
            && reservation.OperationId is { } existingId
            && operations.TryGet(principalSid, existingId, out var existing))
        {
            return new(existing);
        }

        if (reservation.State == AgentIdempotencyReservationState.InProgress)
        {
            return NotAttempted(mode, correlationId, "maintenance.operation_in_progress", "An identical maintenance operation is already in progress.");
        }

        if (reservation.State == AgentIdempotencyReservationState.Conflict)
        {
            return NotAttempted(mode, correlationId, "maintenance.idempotency_conflict", "The idempotency key is already bound to another maintenance challenge.");
        }

        if (reservation.State == AgentIdempotencyReservationState.Capacity)
        {
            return NotAttempted(mode, correlationId, "maintenance.operation_queue_full", "The Agent cannot retain another maintenance operation at this time.");
        }

        var reservationHeld = true;
        AgentOperationLease? lease = null;
        try
        {
            if (!challenges.TryGet(principalSid, challengeId!, out var challenge, out var challengeFailure)
                || challenge is null)
            {
                idempotency.Release(principalSid, mode, idempotencyKey!);
                reservationHeld = false;
                return NotAttempted(mode, correlationId, MapChallengeFailure(challengeFailure), "The maintenance preview is no longer valid; build a new preview.");
            }

            var expectedMode = challenge.Intent.Mode == MaintenanceMode.Cleanup ? "cleanup" : "branch-reset";
            if (!string.Equals(expectedMode, mode, StringComparison.Ordinal))
            {
                idempotency.Release(principalSid, mode, idempotencyKey!);
                reservationHeld = false;
                return NotAttempted(mode, correlationId, "maintenance.challenge_changed", "The maintenance preview does not match this operation.");
            }

            var descriptor = mode == "cleanup"
                ? MaintenanceOperation.CleanupDescriptor
                : MaintenanceOperation.BranchResetDescriptor;
            if (!TryConsumeMutationToken(context, principalSid, descriptor))
            {
                idempotency.Release(principalSid, mode, idempotencyKey!);
                reservationHeld = false;
                return new(null, MaintenanceSecurityFailure.MutationTokenInvalid);
            }

            lease = concurrency.TryEnter();
            if (lease is null)
            {
                idempotency.Release(principalSid, mode, idempotencyKey!);
                reservationHeld = false;
                return NotAttempted(mode, correlationId, "maintenance.operation_busy", "Another maintenance operation is already in progress.");
            }

            if (!challenges.TryConsume(
                    principalSid,
                    challengeId!,
                    mode,
                    challenge.Intent.Fingerprint,
                    typedConfirmation!,
                    out _,
                    out challengeFailure))
            {
                idempotency.Release(principalSid, mode, idempotencyKey!);
                reservationHeld = false;
                lease.Dispose();
                    return NotAttempted(mode, correlationId, MapChallengeFailure(challengeFailure), "The maintenance preview is no longer valid; build a new preview.");
            }

            MaintenanceOperationHandle handle;
            try
            {
                handle = operations.Create(principalSid, mode == "cleanup" ? "cleanup" : "branch-reset", correlationId);
            }
            catch (MaintenanceOperationCapacityException)
            {
                idempotency.Release(principalSid, mode, idempotencyKey!);
                reservationHeld = false;
                lease.Dispose();
                return NotAttempted(mode, correlationId, "maintenance.operation_queue_full", "The Agent cannot retain another maintenance operation at this time.");
            }

            idempotency.Bind(principalSid, mode, idempotencyKey!, handle.OperationId);
            reservationHeld = false;
            operations.Start(handle.OperationId);
            _ = RunAsync(
                principalSid,
                handle.OperationId,
                mode,
                challenge.Intent.Fingerprint,
                lease,
                correlationId,
                CancellationToken.None);
            lease = null;
            return new(handle.InitialState with
            {
                State = MaintenanceOperationStateDto.Running,
                Detail = "The Agent is running the server-owned maintenance operation.",
                ProgressPercent = 1,
                Stage = "running"
            });
        }
        finally
        {
            if (reservationHeld)
            {
                idempotency.Release(principalSid, mode, idempotencyKey!);
            }

            lease?.Dispose();
        }
    }

    private async Task RunAsync(
        string principalSid,
        string operationId,
        string mode,
        string expectedFingerprint,
        AgentOperationLease lease,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var progress = new Progress<string>(_ =>
                operations.Progress(
                    operationId,
                    50,
                    "running",
                    "The Agent is applying the server-owned maintenance policy."));
            var runtime = await settingsFactory.LoadAsync(cancellationToken).ConfigureAwait(false);
            var result = mode == "cleanup"
                ? await maintenance.ExecuteCleanupAsync(runtime.Settings, expectedFingerprint, progress, cancellationToken).ConfigureAwait(false)
                : await maintenance.ExecuteBranchResetAsync(runtime.Settings, expectedFingerprint, progress, cancellationToken).ConfigureAwait(false);

            var evidence = ToContractOutcome(result.Evidence);
            var (state, code, detail) = result.Operation.Status switch
            {
                OperationStatus.Success => (MaintenanceOperationStateDto.Completed, "maintenance.completed", "The maintenance operation completed."),
                OperationStatus.Cancelled when !result.Evidence.DestructiveAttempted =>
                    (MaintenanceOperationStateDto.Failed, MaintenanceFailureCodes.CancelledBeforeDestructiveWork, "Maintenance was cancelled before destructive work began."),
                _ => (MaintenanceOperationStateDto.Failed, result.FailureCode ?? MaintenanceFailureCodes.PartialFailure, "The maintenance operation completed with a failure or recovery condition.")
            };
            operations.Complete(operationId, state, code, detail, evidence);
            AgentAuditRecorder.Record(audit, principalSid, "maintenance." + mode, operationId, correlationId, state.ToString(), code);
        }
        catch (OperationCanceledException)
        {
            operations.Complete(
                operationId,
                MaintenanceOperationStateDto.Failed,
                MaintenanceFailureCodes.RecoveryRequired,
                "The maintenance operation was cancelled; verify the affected installation before retrying.",
                new(true, true, [], [], [MaintenanceFailureCodes.RecoveryGuidance]));
            AgentAuditRecorder.Record(audit, principalSid, "maintenance." + mode, operationId, correlationId, MaintenanceOperationStateDto.Failed.ToString(), MaintenanceFailureCodes.RecoveryRequired);
        }
        catch
        {
            operations.Complete(
                operationId,
                MaintenanceOperationStateDto.OutcomeUnknown,
                "maintenance.outcome_unknown",
                "The maintenance operation outcome is unknown. Verify the affected installation before retrying.",
                new(true, true, [], [], [MaintenanceFailureCodes.RecoveryGuidance]));
            AgentAuditRecorder.Record(audit, principalSid, "maintenance." + mode, operationId, correlationId, MaintenanceOperationStateDto.OutcomeUnknown.ToString(), "maintenance.outcome_unknown");
        }
        finally
        {
            lease.Dispose();
        }
    }

    private static CleanupPreviewDto ToCleanupDto(
        CleanupPreviewBuildResult preview,
        MaintenanceChallenge? challenge)
    {
        var challengeId = challenge?.ChallengeId ?? string.Empty;
        var expires = challenge?.ExpiresAtUtc ?? DateTimeOffset.UtcNow;
        return new(
            challengeId,
            preview.Services.Select((_, index) => $"service-{index + 1:D3}").ToArray(),
            preview.Targets.Where(target => target.Accepted).Select(target => target.TargetId).ToArray(),
            challenge?.Intent.ConfirmationText ?? string.Empty,
            expires)
        {
            Ready = challenge is not null && preview.Ready,
            Targets = preview.Targets.Select(target => new CleanupTargetPreviewDto(
                target.TargetId,
                target.Accepted,
                target.Exists,
                target.IsDirectory,
                target.LengthBytes,
                target.ChildCount,
                target.RejectionCode)).ToArray(),
            Rejections = preview.Rejections.Select(ToRejection).ToArray(),
            Warnings = SafeWarnings(preview.Warnings),
            AvailableFreeSpaceBytes = preview.AvailableFreeSpaceBytes
        };
    }

    private static BranchResetPreviewDto ToBranchResetDto(
        BranchResetPreviewBuildResult preview,
        MaintenanceChallenge? challenge)
    {
        var challengeId = challenge?.ChallengeId ?? string.Empty;
        var expires = challenge?.ExpiresAtUtc ?? DateTimeOffset.UtcNow;
        return new(
            challengeId,
            challenge?.Intent.BranchCode ?? string.Empty,
            preview.Tables.Select(table => table.TableName).ToArray(),
            challenge?.Intent.ConfirmationText ?? string.Empty,
            expires)
        {
            Ready = challenge is not null && preview.Ready,
            DatabaseName = preview.Intent?.DatabaseName ?? string.Empty,
            TableScopes = preview.Tables.Select(table => new BranchResetTablePreviewDto(table.TableName, table.MatchingRows)).ToArray(),
            Rejections = preview.Rejections.Select(ToRejection).ToArray(),
            Warnings = SafeWarnings(preview.Warnings),
            AvailableFreeSpaceBytes = preview.AvailableFreeSpaceBytes
        };
    }

    private static MaintenanceOperationOutcomeDto ToContractOutcome(MaintenanceExecutionEvidence evidence)
    {
        var serviceIndex = 0;
        var items = evidence.Items.Select(item =>
        {
            var targetId = string.Equals(item.Kind, "service", StringComparison.OrdinalIgnoreCase)
                ? $"service-{++serviceIndex:D3}"
                : SafeLogical(item.TargetId) ?? "item-target";
            return new MaintenanceItemOutcomeDto(
                targetId,
                SafeLogical(item.Kind) ?? "item",
                item.State switch
                {
                    "already_absent" => MaintenanceItemState.AlreadyAbsent,
                    "completed" => MaintenanceItemState.Completed,
                    "rejected" => MaintenanceItemState.Rejected,
                    "recovery_required" => MaintenanceItemState.RecoveryRequired,
                    "failed" => MaintenanceItemState.Failed,
                    _ => MaintenanceItemState.NotAttempted
                },
                item.Attempted,
                item.Completed,
                item.ResidueUncertain,
                SafeLogical(item.FailureCode),
                string.IsNullOrWhiteSpace(item.RecoveryGuidance) ? null : MaintenanceFailureCodes.RecoveryGuidance);
        }).ToArray();

        return new(
            evidence.DestructiveAttempted,
            evidence.RecoveryRequired,
            items,
            SafeWarnings(evidence.Warnings),
            evidence.RecoveryGuidance.Count == 0 ? [] : [MaintenanceFailureCodes.RecoveryGuidance]);
    }

    private static MaintenancePolicyRejectionDto ToRejection(MaintenancePolicyRejection rejection) =>
        new(SafeLogical(rejection.TargetId) ?? "target", SafeLogical(rejection.Code) ?? "maintenance.rejected", "The configured maintenance target was rejected by server policy.");

    private static IReadOnlyList<string> SafeWarnings(IReadOnlyList<string> warnings) =>
        warnings.Count == 0 ? [] : ["Some server-owned maintenance evidence is incomplete; review the operation result before retrying."];

    private static string? SafeLogical(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : new string(value.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.').Take(128).ToArray());

    private bool TryConsumeMutationToken(
        HttpContext context,
        string principalSid,
        MutationOperationDescriptor descriptor)
    {
        var tokenValues = context.Request.Headers[MutationTokenContract.HeaderName];
        var originValues = context.Request.Headers.Origin;
        if (tokenValues.Count != 1
            || originValues.Count != 1
            || string.IsNullOrWhiteSpace(tokenValues[0])
            || string.IsNullOrWhiteSpace(originValues[0]))
        {
            return false;
        }

        if (!descriptor.TryResolveHttpPath(null, out var expectedPath)
            || !string.Equals(context.Request.Method, descriptor.HttpMethod, StringComparison.Ordinal)
            || !string.Equals(context.Request.Path.Value, expectedPath, StringComparison.Ordinal))
        {
            return false;
        }

        return mutationTokens.TryConsume(new MutationTokenValidationRequest(
            tokenValues[0]!,
            principalSid,
            originValues[0]!,
            context.Request.Method,
            descriptor.OperationId,
            context.Request.Path.Value)).Succeeded;
    }

    private static string MapChallengeFailure(MaintenanceChallengeFailure failure) => failure switch
    {
        MaintenanceChallengeFailure.Expired => "maintenance.challenge_expired",
        MaintenanceChallengeFailure.Used => "maintenance.challenge_used",
        MaintenanceChallengeFailure.WrongPrincipal => "maintenance.challenge_principal_mismatch",
        MaintenanceChallengeFailure.Changed => "maintenance.challenge_changed",
        _ => "maintenance.challenge_not_found"
    };

    private static MaintenanceAgentExecutionResult NotAttempted(
        string mode,
        string correlationId,
        string code,
        string detail) =>
        new(new MaintenanceOperationDto(
            string.Empty,
            mode,
            MaintenanceOperationStateDto.NotAttempted,
            MaintenanceOperationStateDto.NotAttempted,
            0,
            "not-attempted",
            detail,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            new(false, false, [], [], []),
            code,
            correlationId));

    private static CleanupPreviewDto ToCleanupError(string correlationId, string code, string detail) =>
        new(string.Empty, [], [], string.Empty, DateTimeOffset.UtcNow)
        {
            Ready = false,
            Rejections = [new("preview", code, detail)]
        };

    private static BranchResetPreviewDto ToBranchResetError(string correlationId, string code, string detail) =>
        new(string.Empty, string.Empty, [], string.Empty, DateTimeOffset.UtcNow)
        {
            Ready = false,
            Rejections = [new("preview", code, detail)]
        };
}
