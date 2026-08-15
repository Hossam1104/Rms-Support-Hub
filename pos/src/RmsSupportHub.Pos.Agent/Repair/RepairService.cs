using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Packages;
using RmsSupportHub.Pos.Agent.Runtime;
using RmsSupportHub.Pos.Contracts.V1.Packages;
using RmsSupportHub.Pos.Contracts.V1.Repair;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Application.Repair;

namespace RmsSupportHub.Pos.Agent.Repair;

public static class RepairOperation
{
    public const string OperationId = "repair.installation.execute";
    public const string HttpMethod = "POST";
    public const string HttpPath = "/api/v1/repair/operations";

    public static MutationTokens.MutationOperationDescriptor Descriptor { get; } =
        new(OperationId, HttpMethod, HttpPath, MutationTokens.MutationTargetKind.None);
}

public static class GuidedRepairOperation
{
    public const string OperationId = "repair.guided.checkpoint";
    public const string HttpMethod = "POST";
    public const string HttpPath = "/api/v1/repair/guided/steps";

    public static MutationTokens.MutationOperationDescriptor Descriptor { get; } =
        new(OperationId, HttpMethod, HttpPath, MutationTokens.MutationTargetKind.None);
}

public sealed class RepairPreviewStore(
    RuntimeRetentionPolicy retention,
    TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public string? Add(string principalSid, RepairOperationKind operation, AgentPackageManifest manifest, string? snapshotId, string confirmation, DateTimeOffset expiresAtUtc)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.Count >= retention.MaxCompletedOperations) return null;
            var id = Guid.NewGuid().ToString("N");
            entries[id] = new(principalSid, operation, manifest, snapshotId, confirmation, expiresAtUtc);
            return id;
        }
    }

    public bool TryGet(string principalSid, string previewId, out RepairPreviewEntry? entry)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.TryGetValue(previewId, out var found)
                && string.Equals(found.PrincipalSid, principalSid, StringComparison.Ordinal)
                && clock.GetUtcNow() < found.ExpiresAtUtc)
            {
                entry = new(found.PrincipalSid, found.Operation, found.Manifest, found.SnapshotId, found.Confirmation, found.ExpiresAtUtc);
                return true;
            }

            entry = null;
            return false;
        }
    }

    public bool TryTake(string principalSid, string previewId, out RepairPreviewEntry? entry)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.Remove(previewId, out var found)
                && string.Equals(found.PrincipalSid, principalSid, StringComparison.Ordinal)
                && clock.GetUtcNow() < found.ExpiresAtUtc)
            {
                entry = new(found.PrincipalSid, found.Operation, found.Manifest, found.SnapshotId, found.Confirmation, found.ExpiresAtUtc);
                return true;
            }

            entry = null;
            return false;
        }
    }

    private void PruneLocked(DateTimeOffset now)
    {
        foreach (var pair in entries.ToArray())
        {
            if (now >= pair.Value.ExpiresAtUtc) entries.Remove(pair.Key);
        }
    }

    private sealed record Entry(
        string PrincipalSid,
        RepairOperationKind Operation,
        AgentPackageManifest Manifest,
        string? SnapshotId,
        string Confirmation,
        DateTimeOffset ExpiresAtUtc);
}

public sealed record RepairPreviewEntry(
    string PrincipalSid,
    RepairOperationKind Operation,
    AgentPackageManifest Manifest,
    string? SnapshotId,
    string Confirmation,
    DateTimeOffset ExpiresAtUtc);

public sealed class RepairOperationStore(
    RuntimeRetentionPolicy retention,
    TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public RepairOperationDto Add(string principalSid, RepairOperationKind operation, DateTimeOffset startedAtUtc, string correlationId)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.Count >= retention.MaxCompletedOperations) throw new InvalidOperationException("The repair operation retention limit has been reached.");
            var operationId = Guid.NewGuid().ToString("N");
            var response = new RepairOperationDto(
                operationId,
                MapRepairOperation(operation),
                RepairOperationStateDto.Accepted,
                RepairOperationStateDto.Accepted,
                0,
                "accepted",
                "The typed repair operation was accepted by the Agent.",
                false,
                false,
                false,
                startedAtUtc,
                null,
                correlationId);
            entries[operationId] = new(principalSid, response);
            return response;
        }
    }

    public bool TryGet(string principalSid, string operationId, out RepairOperationDto? response)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.TryGetValue(operationId, out var entry)
                && string.Equals(entry.PrincipalSid, principalSid, StringComparison.Ordinal))
            {
                response = entry.Response;
                return true;
            }

            response = null;
            return false;
        }
    }

    public bool Update(string principalSid, string operationId, Func<RepairOperationDto, RepairOperationDto> update)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(operationId, out var entry)
                || !string.Equals(entry.PrincipalSid, principalSid, StringComparison.Ordinal)) return false;
            entries[operationId] = entry with { Response = update(entry.Response) };
            return true;
        }
    }

    private void PruneLocked(DateTimeOffset now)
    {
        foreach (var pair in entries.ToArray())
        {
            if (pair.Value.Response.CompletedAtUtc is { } completed
                && now - completed >= retention.CompletedOperationLifetime) entries.Remove(pair.Key);
        }
    }

    private sealed record Entry(string PrincipalSid, RepairOperationDto Response);

    private static RepairOperationKindDto MapRepairOperation(RepairOperationKind value) =>
        Enum.TryParse<RepairOperationKindDto>(value.ToString(), out var mapped) ? mapped : RepairOperationKindDto.Guided;
}

public sealed class GuidedRepairStore(
    RuntimeRetentionPolicy retention,
    TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public string? Add(string principalSid, GuidedRepairState state)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.Count >= retention.MaxCompletedOperations) return null;
            var id = Guid.NewGuid().ToString("N");
            entries[id] = new(principalSid, state);
            return id;
        }
    }

    public bool TryGet(string principalSid, string id, out GuidedRepairState? state)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.TryGetValue(id, out var entry)
                && string.Equals(entry.PrincipalSid, principalSid, StringComparison.Ordinal))
            {
                state = entry.State;
                return true;
            }

            state = null;
            return false;
        }
    }

    public bool Update(string principalSid, string id, Func<GuidedRepairState, GuidedRepairState> update)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(id, out var entry)
                || !string.Equals(entry.PrincipalSid, principalSid, StringComparison.Ordinal)) return false;
            entries[id] = entry with { State = update(entry.State) };
            return true;
        }
    }

    private void PruneLocked(DateTimeOffset now)
    {
        foreach (var pair in entries.ToArray())
        {
            if (now >= pair.Value.State.ExpiresAtUtc) entries.Remove(pair.Key);
        }
    }

    private sealed record Entry(string PrincipalSid, GuidedRepairState State);
}

public sealed record GuidedRepairState(
    RepairOperationStateDto State,
    string? SnapshotId,
    string? CurrentStepId,
    IReadOnlyList<GuidedRepairStepDto> Steps,
    string Detail,
    DateTimeOffset ExpiresAtUtc);

public sealed class RepairService(
    AgentPackageService packages,
    ISafetySnapshotStore snapshots,
    RepairPreviewStore previews,
    RepairOperationStore operations,
    GuidedRepairStore guided,
    AgentScopedIdempotencyStore idempotency,
    IAgentPackageLifecycle lifecycle,
    IPrivilegedMutationLease privilegedLease,
    IncidentTimelineService timeline,
    TimeProvider clock)
{
    private const string IdempotencyScope = "repair.installation";
    private const string GuidedIdempotencyScope = "repair.guided";
    private const string PreviewIdempotencyScope = "repair.preview";

    public async Task<RepairPreviewDto> PreviewAsync(
        string principalSid,
        RepairOperationKind operation,
        string? snapshotId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!AgentScopedIdempotencyStore.IsValidKey(idempotencyKey))
        {
            throw new RepairRejectedException("repair_preview_idempotency_invalid");
        }

        var material = operation + "|" + (snapshotId ?? string.Empty);
        var reservation = idempotency.TryReserve(principalSid, PreviewIdempotencyScope, idempotencyKey, material);
        if (reservation.State == AgentIdempotencyReservationState.Completed
            && reservation.OperationId is not null
            && previews.TryGet(principalSid, reservation.OperationId, out var existing)
            && existing is not null)
        {
            return ToPreview(reservation.OperationId, existing);
        }
        if (reservation.State != AgentIdempotencyReservationState.Reserved)
        {
            throw new RepairRejectedException(reservation.State switch
            {
                AgentIdempotencyReservationState.InProgress => "repair_preview_in_progress",
                AgentIdempotencyReservationState.Capacity => "repair_preview_capacity",
                _ => "repair_preview_idempotency_conflict"
            });
        }

        try
        {
            var now = clock.GetUtcNow();
            var expires = now.AddMinutes(5);
            var snapshot = await VerifySnapshotAsync(principalSid, snapshotId, cancellationToken).ConfigureAwait(false);
            var packageOperation = operation == RepairOperationKind.Guided ? AgentPackageOperationKind.Repair : MapPackageOperation(operation);
            var (manifest, validation) = await packages.VerifyForRepairAsync(packageOperation, cancellationToken).ConfigureAwait(false);
            var blockers = new List<string>();
            if (!snapshot.Verified) blockers.Add(snapshot.SnapshotId is null ? "fresh_snapshot_required" : "fresh_snapshot_not_verified");
            if (validation.State != AgentPackageVerificationState.Verified) blockers.AddRange(validation.Blockers);
            if (snapshot.Verified && snapshot.Snapshot is { Capacity.State: not SafetySnapshotEvidenceState.Healthy }) blockers.Add("capacity_precondition_not_healthy");

            if (manifest is null)
            {
                idempotency.Release(principalSid, PreviewIdempotencyScope, idempotencyKey);
                return new(string.Empty, MapRepairOperation(operation), false, Map(validation.State), snapshot.Reference, EffectsFor(operation), blockers.Distinct(StringComparer.Ordinal).ToArray(), ConfirmationFor(operation), expires);
            }

            var confirmation = ConfirmationFor(operation);
            var ready = blockers.Count == 0 && validation.State == AgentPackageVerificationState.Verified;
            var previewId = ready ? previews.Add(principalSid, operation, manifest, snapshotId, confirmation, expires) : null;
            if (ready && previewId is null) blockers.Add("repair_preview_capacity");
            if (previewId is null)
            {
                idempotency.Release(principalSid, PreviewIdempotencyScope, idempotencyKey);
            }
            else
            {
                idempotency.Bind(principalSid, PreviewIdempotencyScope, idempotencyKey, previewId);
            }
            return new(previewId ?? string.Empty, MapRepairOperation(operation), ready && previewId is not null, Map(validation.State), snapshot.Reference, EffectsFor(operation), blockers.Distinct(StringComparer.Ordinal).ToArray(), confirmation, expires);
        }
        catch
        {
            idempotency.Release(principalSid, PreviewIdempotencyScope, idempotencyKey);
            throw;
        }
    }

    private static RepairPreviewDto ToPreview(string previewId, RepairPreviewEntry entry) =>
        new(previewId, MapRepairOperation(entry.Operation), true, AgentPackageVerificationStateDto.Verified, new(entry.SnapshotId, "Verified", true, entry.ExpiresAtUtc, "The retained repair preview remains valid."), EffectsFor(entry.Operation), [], entry.Confirmation, entry.ExpiresAtUtc);

    public async Task<RepairOperationDto> ExecuteAsync(
        string principalSid,
        RepairExecuteRequestDto request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!IsOpaqueId(request.PreviewId)
            || !AgentScopedIdempotencyStore.IsValidKey(request.IdempotencyKey)
            || request.TypedConfirmation is null
            || request.TypedConfirmation.Length > 128)
        {
            throw new RepairRejectedException("repair_request_invalid");
        }

        var reservation = idempotency.TryReserve(
            principalSid,
            IdempotencyScope,
            request.IdempotencyKey,
            request.PreviewId + "|" + request.TypedConfirmation + "|" + (request.SnapshotId ?? string.Empty));
        if (reservation.State == AgentIdempotencyReservationState.Completed
            && reservation.OperationId is not null
            && operations.TryGet(principalSid, reservation.OperationId, out var existing)
            && existing is not null) return existing;
        if (reservation.State != AgentIdempotencyReservationState.Reserved)
        {
            throw new RepairRejectedException(reservation.State switch
            {
                AgentIdempotencyReservationState.InProgress => "repair_request_in_progress",
                AgentIdempotencyReservationState.Capacity => "repair_operation_capacity",
                _ => "repair_idempotency_conflict"
            });
        }

        if (!TryGetAndValidatePreview(principalSid, request, out var preview))
        {
            idempotency.Release(principalSid, IdempotencyScope, request.IdempotencyKey);
            throw new RepairRejectedException("repair_preview_invalid");
        }

        var selectedPreview = preview!;
        var snapshotId = request.SnapshotId ?? selectedPreview.SnapshotId;
        var verification = await VerifySnapshotAsync(principalSid, snapshotId, cancellationToken).ConfigureAwait(false);
        if (!verification.Verified)
        {
            idempotency.Release(principalSid, IdempotencyScope, request.IdempotencyKey);
            throw new RepairRejectedException("fresh_snapshot_not_verified");
        }

        if (!previews.TryTake(principalSid, request.PreviewId, out preview) || preview is null)
        {
            idempotency.Release(principalSid, IdempotencyScope, request.IdempotencyKey);
            throw new RepairRejectedException("repair_preview_replayed");
        }

        var accepted = operations.Add(principalSid, preview.Operation, clock.GetUtcNow(), correlationId);
        idempotency.Bind(principalSid, IdempotencyScope, request.IdempotencyKey, accepted.OperationId);
        _ = Task.Run(() => ExecuteLifecycleAsync(principalSid, accepted.OperationId, preview, snapshotId, correlationId), CancellationToken.None);
        return accepted;
    }

    public bool TryGetOperation(string principalSid, string operationId, out RepairOperationDto? response)
    {
        if (IsOpaqueId(operationId)) return operations.TryGet(principalSid, operationId, out response);
        response = null;
        return false;
    }

    public async Task<GuidedRepairDto> BeginGuidedAsync(string principalSid, string? snapshotId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (!AgentScopedIdempotencyStore.IsValidKey(idempotencyKey))
        {
            throw new RepairRejectedException("guided_preview_idempotency_invalid");
        }

        var material = "guided|" + (snapshotId ?? string.Empty);
        var reservation = idempotency.TryReserve(principalSid, GuidedIdempotencyScope, idempotencyKey, material);
        if (reservation.State == AgentIdempotencyReservationState.Completed
            && reservation.OperationId is not null
            && guided.TryGet(principalSid, reservation.OperationId, out var existing)
            && existing is not null)
        {
            return ToDto(reservation.OperationId, existing);
        }
        if (reservation.State != AgentIdempotencyReservationState.Reserved)
        {
            throw new RepairRejectedException(reservation.State switch
            {
                AgentIdempotencyReservationState.InProgress => "guided_preview_in_progress",
                AgentIdempotencyReservationState.Capacity => "guided_preview_capacity",
                _ => "guided_preview_idempotency_conflict"
            });
        }

        try
        {
            var verification = await VerifySnapshotAsync(principalSid, snapshotId, cancellationToken).ConfigureAwait(false);
            var steps = GuidedRepairWorkflow.Checkpoints.Select((checkpoint, index) => new GuidedRepairStepDto(
                checkpoint.StepId,
                checkpoint.Title,
                checkpoint.Description,
                index == 0 && verification.Verified ? GuidedRepairStepStateDto.Ready : index == 0 ? GuidedRepairStepStateDto.Blocked : GuidedRepairStepStateDto.Pending,
                true,
                ConfirmationForStep(checkpoint.StepId),
                index == 0 && !verification.Verified ? "fresh_snapshot_not_verified" : null)).ToArray();
            var expires = clock.GetUtcNow().AddMinutes(15);
            var state = new GuidedRepairState(
                RepairOperationStateDto.Preview,
                snapshotId,
                null,
                steps,
                verification.Verified ? "Guided Repair is waiting for typed checkpoint confirmation." : verification.Reference.Detail,
                expires);
            var id = guided.Add(principalSid, state);
            if (id is null)
            {
                idempotency.Release(principalSid, GuidedIdempotencyScope, idempotencyKey);
                return new(string.Empty, RepairOperationStateDto.OutcomeUnknown, [], "Guided Repair retention is full.", expires);
            }

            idempotency.Bind(principalSid, GuidedIdempotencyScope, idempotencyKey, id);
            return ToDto(id, state);
        }
        catch
        {
            idempotency.Release(principalSid, GuidedIdempotencyScope, idempotencyKey);
            throw;
        }
    }

    public bool TryGetGuided(string principalSid, string guidedId, out GuidedRepairDto? response)
    {
        if (!IsOpaqueId(guidedId) || !guided.TryGet(principalSid, guidedId, out var state) || state is null)
        {
            response = null;
            return false;
        }

        response = ToDto(guidedId, state);
        return true;
    }

    public async Task<GuidedRepairDto> AdvanceGuidedAsync(
        string principalSid,
        GuidedRepairStepRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!IsOpaqueId(request.GuidedRepairId)
            || !AgentScopedIdempotencyStore.IsValidKey(request.IdempotencyKey)
            || request.StepId is not { Length: > 0 and <= 64 }
            || request.StepId.Any(char.IsControl)
            || request.TypedConfirmation is not { Length: > 0 and <= 128 }
            || request.TypedConfirmation.Any(char.IsControl)) throw new RepairRejectedException("guided_request_invalid");
        var reservation = idempotency.TryReserve(principalSid, GuidedIdempotencyScope, request.IdempotencyKey, request.GuidedRepairId + "|" + request.StepId + "|" + request.TypedConfirmation);
        if (reservation.State == AgentIdempotencyReservationState.Completed
            && reservation.OperationId is not null
            && guided.TryGet(principalSid, reservation.OperationId, out var existing)
            && existing is not null)
        {
            return ToDto(reservation.OperationId, existing);
        }
        if (reservation.State != AgentIdempotencyReservationState.Reserved)
        {
            throw new RepairRejectedException("guided_idempotency_conflict");
        }

        if (!guided.TryGet(principalSid, request.GuidedRepairId, out var current) || current is null)
        {
            idempotency.Release(principalSid, GuidedIdempotencyScope, request.IdempotencyKey);
            throw new RepairRejectedException("guided_repair_unavailable");
        }

        var currentStep = current.Steps.FirstOrDefault(step => string.Equals(step.StepId, request.StepId, StringComparison.Ordinal));
        var retryingBlockedStep = current.CurrentStepId is not null
            && string.Equals(current.CurrentStepId, request.StepId, StringComparison.Ordinal)
            && currentStep?.State == GuidedRepairStepStateDto.Blocked;
        if ((!retryingBlockedStep && !GuidedRepairWorkflow.IsValidTransition(current.CurrentStepId, request.StepId))
            || !string.Equals(request.TypedConfirmation, ConfirmationForStep(request.StepId), StringComparison.Ordinal))
        {
            idempotency.Release(principalSid, GuidedIdempotencyScope, request.IdempotencyKey);
            throw new RepairRejectedException("guided_checkpoint_invalid");
        }

        IDisposable? privilegedHandle = null;
        try
        {
            if (request.StepId is "stage-package" or "activate-package" or "health-verified")
            {
                var attempt = privilegedLease.TryAcquire("repair.guided." + request.StepId, principalSid);
                if (attempt.State != PrivilegedMutationLeaseState.Acquired || attempt.Handle is null)
                {
                    idempotency.Release(principalSid, GuidedIdempotencyScope, request.IdempotencyKey);
                    throw new RepairRejectedException(attempt.State == PrivilegedMutationLeaseState.Busy
                        ? "privileged_mutation_busy"
                        : "privileged_mutation_lease_unavailable");
                }

                privilegedHandle = attempt.Handle;
            }

            var checkpointIndex = Array.FindIndex(GuidedRepairWorkflow.Checkpoints.ToArray(), item => item.StepId == request.StepId);
            var outcome = await EvaluateCheckpointAsync(principalSid, current, request.StepId, checkpointIndex, cancellationToken).ConfigureAwait(false);
            var updatedSteps = current.Steps.Select(step => step.StepId == request.StepId ? step with
            {
                State = outcome.Completed ? GuidedRepairStepStateDto.Completed : GuidedRepairStepStateDto.Blocked,
                FailureCode = outcome.Completed ? null : outcome.FailureCode
            } : step).ToArray();
            if (outcome.Completed && checkpointIndex + 1 < updatedSteps.Length)
            {
                var next = updatedSteps[checkpointIndex + 1];
                updatedSteps[checkpointIndex + 1] = next with { State = GuidedRepairStepStateDto.Ready };
            }

            var workflowState = outcome.Completed && checkpointIndex == updatedSteps.Length - 1
                ? RepairOperationStateDto.Completed
                : outcome.Completed ? RepairOperationStateDto.Preview : RepairOperationStateDto.Partial;
            var nextState = current with
            {
                State = workflowState,
                CurrentStepId = request.StepId,
                Steps = updatedSteps,
                Detail = outcome.Detail
            };
            guided.Update(principalSid, request.GuidedRepairId, _ => nextState);
            idempotency.Bind(principalSid, GuidedIdempotencyScope, request.IdempotencyKey, request.GuidedRepairId);
            return ToDto(request.GuidedRepairId, nextState);
        }
        finally
        {
            privilegedHandle?.Dispose();
        }
    }

    private async Task<CheckpointOutcome> EvaluateCheckpointAsync(
        string principalSid,
        GuidedRepairState state,
        string stepId,
        int stepIndex,
        CancellationToken cancellationToken)
    {
        if (stepId == "snapshot-verified")
        {
            var snapshot = await VerifySnapshotAsync(principalSid, state.SnapshotId, cancellationToken).ConfigureAwait(false);
            return snapshot.Verified
                ? new(true, null, "The safety snapshot is fresh, principal-scoped, and integrity verified.")
                : new(false, "fresh_snapshot_not_verified", snapshot.Reference.Detail);
        }

        if (stepId == "package-verified")
        {
            var (_, validation) = await packages.VerifyForRepairAsync(AgentPackageOperationKind.Repair, cancellationToken).ConfigureAwait(false);
            return validation.State == AgentPackageVerificationState.Verified
                ? new(true, null, "The server-owned repair package passed policy, checksum, and signature verification.")
                : new(false, "package_verification_failed", validation.Detail);
        }

        if (stepId == "capacity-verified")
        {
            var snapshot = await VerifySnapshotAsync(principalSid, state.SnapshotId, cancellationToken).ConfigureAwait(false);
            return snapshot.Verified && snapshot.Snapshot?.Capacity.State == SafetySnapshotEvidenceState.Healthy
                ? new(true, null, "The snapshot capacity precondition is healthy.")
                : new(false, "capacity_precondition_not_healthy", "The fixed Agent capacity precondition is not healthy.");
        }

        // Staging and activation are deliberately not inferred from a checkpoint click. The
        // typed package operation owns that boundary and reports health/rollback truth.
        return new(false, "typed_package_operation_required", "This checkpoint is coordinated by the explicit typed repair operation; Guided Repair will not activate a package implicitly.");
    }

    private async Task ExecuteLifecycleAsync(string principalSid, string operationId, RepairPreviewEntry preview, string? snapshotId, string correlationId)
    {
        operations.Update(principalSid, operationId, current => current with
        {
            State = RepairOperationStateDto.Running,
            Outcome = RepairOperationStateDto.Running,
            ProgressPercent = 20,
            Stage = "staging",
            Detail = "The verified repair package is being staged inside the fixed Agent boundary."
        });

        AgentPackageExecutionResult result;
        IDisposable? privilegedHandle = null;
        try
        {
            try
            {
                var leaseAttempt = privilegedLease.TryAcquire("repair." + preview.Operation, principalSid);
                if (leaseAttempt.State != PrivilegedMutationLeaseState.Acquired || leaseAttempt.Handle is null)
                {
                    result = leaseAttempt.State == PrivilegedMutationLeaseState.Busy
                        ? new(AgentPackageOperationState.Busy, false, false, false, leaseAttempt.Detail)
                        : new(AgentPackageOperationState.Failed, false, false, true, leaseAttempt.Detail);
                }
                else
                {
                    privilegedHandle = leaseAttempt.Handle;
                    result = await lifecycle.ExecuteAsync(new(
                        preview.Operation == RepairOperationKind.Guided ? AgentPackageOperationKind.Repair : MapPackageOperation(preview.Operation),
                        preview.Manifest,
                        snapshotId,
                        principalSid,
                        correlationId), CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch
            {
                result = new(AgentPackageOperationState.OutcomeUnknown, false, false, true, "The repair lifecycle outcome could not be determined safely.");
            }

            var outcome = Map(result.State);
            operations.Update(principalSid, operationId, current => current with
            {
                State = outcome,
                Outcome = outcome,
                ProgressPercent = 100,
                Stage = "completed",
                Detail = SafeDetail(result.Detail),
                RollbackAttempted = result.RollbackAttempted,
                RollbackSucceeded = result.RollbackSucceeded,
                RecoveryRequired = result.RecoveryRequired,
                CompletedAtUtc = clock.GetUtcNow()
            });
            timeline.Record(principalSid, "Repair", result.RecoveryRequired ? FailureSeverity.ActionRequired : FailureSeverity.Informational, SafeDetail(result.Detail), operationId: RepairOperation.OperationId, correlationId: correlationId);
        }
        finally
        {
            privilegedHandle?.Dispose();
        }
    }

    private bool TryGetAndValidatePreview(string principalSid, RepairExecuteRequestDto request, out RepairPreviewEntry? preview)
    {
        preview = null;
        if (!previews.TryGet(principalSid, request.PreviewId, out var found) || found is null) return false;
        if (!string.Equals(request.TypedConfirmation, found.Confirmation, StringComparison.Ordinal)) return false;
        if (request.SnapshotId is not null && !string.Equals(request.SnapshotId, found.SnapshotId, StringComparison.Ordinal)) return false;
        preview = found;
        return true;
    }

    private async Task<SnapshotCheck> VerifySnapshotAsync(string principalSid, string? snapshotId, CancellationToken cancellationToken)
    {
        if (!IsOpaqueId(snapshotId)) return new(null, new(null, "Unavailable", false, null, "A fresh opaque safety snapshot identifier is required."));
        var result = await snapshots.ReadAndVerifyAsync(principalSid, snapshotId!, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return new(result.Snapshot, new(snapshotId, result.State.ToString(), result.Verified, result.Snapshot?.ExpiresAtUtc, result.Detail));
    }

    private static GuidedRepairDto ToDto(string id, GuidedRepairState state) => new(id, state.State, state.Steps, state.Detail, state.ExpiresAtUtc);

    private static SafetySnapshotReferenceDto SnapshotReference(SafetySnapshotVerificationResult result, string? snapshotId) =>
        new(snapshotId, result.State.ToString(), result.Verified, result.Snapshot?.ExpiresAtUtc, result.Detail);

    private static string ConfirmationFor(RepairOperationKind operation) => operation switch
    {
        RepairOperationKind.Install => "INSTALL VERIFIED RMS AGENT",
        RepairOperationKind.Upgrade => "UPGRADE VERIFIED RMS AGENT",
        RepairOperationKind.Uninstall => "UNINSTALL RMS AGENT",
        RepairOperationKind.Rollback => "ROLLBACK RMS AGENT",
        _ => "REPAIR VERIFIED RMS AGENT"
    };

    private static string ConfirmationForStep(string stepId) => "CONFIRM " + stepId.Replace('-', ' ').ToUpperInvariant();

    private static string[] EffectsFor(RepairOperationKind operation) => operation switch
    {
        RepairOperationKind.Install => ["stage the verified local Agent package", "activate only the owned Agent service boundary", "verify health and report rollback truth"],
        RepairOperationKind.Upgrade => ["retain the prior version", "activate the verified package", "verify health and rollback if confirmed"],
        RepairOperationKind.Uninstall => ["remove only the owned Agent package boundary", "preserve recovery evidence"],
        RepairOperationKind.Rollback => ["restore the prior Agent package", "verify service and certificate health"],
        _ => ["coordinate the fixed Guided Repair checkpoints", "never activate a package from a recommendation alone"]
    };

    private static AgentPackageOperationKind MapPackageOperation(RepairOperationKind value) =>
        Enum.TryParse<AgentPackageOperationKind>(value.ToString(), out var mapped) ? mapped : AgentPackageOperationKind.Repair;

    private static RepairOperationKindDto MapRepairOperation(RepairOperationKind value) =>
        Enum.TryParse<RepairOperationKindDto>(value.ToString(), out var mapped) ? mapped : RepairOperationKindDto.Guided;

    private static RepairOperationStateDto Map(AgentPackageOperationState value) => value switch
    {
        AgentPackageOperationState.Busy => RepairOperationStateDto.Busy,
        AgentPackageOperationState.Completed => RepairOperationStateDto.Completed,
        AgentPackageOperationState.RollbackSucceeded => RepairOperationStateDto.RollbackSucceeded,
        AgentPackageOperationState.RollbackFailed => RepairOperationStateDto.RollbackFailed,
        AgentPackageOperationState.RecoveryRequired => RepairOperationStateDto.RecoveryRequired,
        AgentPackageOperationState.OutcomeUnknown => RepairOperationStateDto.OutcomeUnknown,
        AgentPackageOperationState.Failed => RepairOperationStateDto.Failed,
        _ => RepairOperationStateDto.Partial
    };

    private static AgentPackageVerificationStateDto Map(AgentPackageVerificationState value) =>
        Enum.TryParse<AgentPackageVerificationStateDto>(value.ToString(), out var mapped) ? mapped : AgentPackageVerificationStateDto.Unknown;

    private static string SafeDetail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "The repair operation completed without additional detail." : value.Replace('\r', ' ').Replace('\n', ' ')[..Math.Min(value.Length, 256)];

    private static bool IsOpaqueId(string? value) => value is { Length: 32 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record SnapshotCheck(SafetySnapshotDocument? Snapshot, SafetySnapshotReferenceDto Reference)
    {
        public bool Verified => Reference.Verified;
        public string? SnapshotId => Reference.SnapshotId;
    }

    private sealed record CheckpointOutcome(bool Completed, string? FailureCode, string Detail);
}

public sealed class RepairRejectedException(string code) : InvalidOperationException(code);
