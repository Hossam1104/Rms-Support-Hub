using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Runtime;
using RmsSupportHub.Pos.Contracts.V1.Packages;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Packages;

public static class AgentPackageOperation
{
    public const string OperationId = "agent-package.operation.execute";
    public const string HttpMethod = "POST";
    public const string HttpPath = "/api/v1/packages/operations";

    public static MutationOperationDescriptor Descriptor { get; } =
        new(OperationId, HttpMethod, HttpPath, MutationTargetKind.None);
}

public sealed class AgentPackagePreviewStore(
    RuntimeRetentionPolicy retention,
    TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public string? Add(
        string principalSid,
        AgentPackageOperationKind operation,
        AgentPackageManifest manifest,
        string confirmation,
        DateTimeOffset expiresAtUtc)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.Count >= retention.MaxCompletedOperations) return null;
            var id = Guid.NewGuid().ToString("N");
            entries[id] = new(principalSid, operation, manifest, confirmation, expiresAtUtc);
            return id;
        }
    }

    public bool TryGet(string principalSid, string previewId, out AgentPackagePreviewEntry? entry)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.TryGetValue(previewId, out var found)
                && string.Equals(found.PrincipalSid, principalSid, StringComparison.Ordinal)
                && clock.GetUtcNow() < found.ExpiresAtUtc)
            {
                entry = new(found.PrincipalSid, found.Operation, found.Manifest, found.Confirmation, found.ExpiresAtUtc);
                return true;
            }

            entry = null;
            return false;
        }
    }

    public bool TryTake(string principalSid, string previewId, out AgentPackagePreviewEntry? entry)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.Remove(previewId, out var found)
                && string.Equals(found.PrincipalSid, principalSid, StringComparison.Ordinal)
                && clock.GetUtcNow() < found.ExpiresAtUtc)
            {
                entry = new(found.PrincipalSid, found.Operation, found.Manifest, found.Confirmation, found.ExpiresAtUtc);
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
        AgentPackageOperationKind Operation,
        AgentPackageManifest Manifest,
        string Confirmation,
        DateTimeOffset ExpiresAtUtc);
}

public sealed record AgentPackagePreviewEntry(
    string PrincipalSid,
    AgentPackageOperationKind Operation,
    AgentPackageManifest Manifest,
    string Confirmation,
    DateTimeOffset ExpiresAtUtc);

public sealed class AgentPackageOperationStore(
    RuntimeRetentionPolicy retention,
    TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public AgentPackageOperationDto Add(string principalSid, AgentPackageOperationKind operation, DateTimeOffset startedAtUtc, string correlationId)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.Count >= retention.MaxCompletedOperations) throw new InvalidOperationException("The package operation retention limit has been reached.");
            var operationId = Guid.NewGuid().ToString("N");
            var response = new AgentPackageOperationDto(
                operationId,
                Map(operation),
                AgentPackageOperationStateDto.Accepted,
                AgentPackageOperationStateDto.Accepted,
                0,
                "accepted",
                "The verified package operation was accepted by the Agent.",
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

    public bool TryGet(string principalSid, string operationId, out AgentPackageOperationDto? response)
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

    public bool Update(string principalSid, string operationId, Func<AgentPackageOperationDto, AgentPackageOperationDto> update)
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

    private sealed record Entry(string PrincipalSid, AgentPackageOperationDto Response);

    private static AgentPackageOperationKindDto Map(AgentPackageOperationKind value) =>
        Enum.TryParse<AgentPackageOperationKindDto>(value.ToString(), out var mapped) ? mapped : AgentPackageOperationKindDto.Repair;
}

public sealed class AgentPackageService(
    IAgentPackageCatalog catalog,
    IAgentPackageVerifier verifier,
    IAgentPackageLifecycle lifecycle,
    ISafetySnapshotStore snapshots,
    AgentPackagePreviewStore previews,
    AgentPackageOperationStore operations,
    AgentScopedIdempotencyStore idempotency,
    IncidentTimelineService timeline,
    TimeProvider clock)
{
    private const string IdempotencyScope = "agent-package.operation";

    public async Task<AgentPackageStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var installed = await catalog.GetInstalledAsync(cancellationToken).ConfigureAwait(false);
        if (installed is null)
        {
            return new(null, null, AgentPackageVerificationStateDto.Unknown, AgentPackageOperationStateDto.NotAttempted, null,
                "No server-owned Agent package manifest is installed or available for safe projection.");
        }

        return new(
            installed.Version,
            installed.PreviousVersion,
            AgentPackageVerificationStateDto.Unverified,
            AgentPackageOperationStateDto.NotAttempted,
            ToDto(installed),
            "The installed manifest is visible, but activation trust is re-verified only during an explicit typed preview.");
    }

    public async Task<AgentPackagePreviewDto> PreviewAsync(
        string principalSid,
        AgentPackageOperationKind operation,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(5);
        var manifest = operation == AgentPackageOperationKind.Uninstall
            ? await catalog.GetInstalledAsync(cancellationToken).ConfigureAwait(false)
            : await catalog.GetAvailableAsync(operation, cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            return new(
                string.Empty,
                Map(operation),
                false,
                AgentPackageVerificationStateDto.Unknown,
                null,
                EffectsFor(operation),
                ["server_owned_package_unavailable"],
                ConfirmationFor(operation),
                expires);
        }

        var validation = await verifier.VerifyAsync(manifest, cancellationToken).ConfigureAwait(false);
        if (validation.State != AgentPackageVerificationState.Verified)
        {
            return new(
                string.Empty,
                Map(operation),
                false,
                Map(validation.State),
                ToDto(manifest),
                EffectsFor(operation),
                validation.Blockers.Count == 0 ? ["package_verification_failed"] : validation.Blockers,
                ConfirmationFor(operation),
                expires);
        }

        var confirmation = ConfirmationFor(operation);
        var previewId = previews.Add(principalSid, operation, manifest, confirmation, expires);
        return new(
            previewId ?? string.Empty,
            Map(operation),
            previewId is not null,
            Map(validation.State),
            ToDto(manifest),
            EffectsFor(operation),
            previewId is null ? ["package_preview_capacity"] : [],
            confirmation,
            expires);
    }

    public async Task<AgentPackageOperationDto> StartAsync(
        string principalSid,
        AgentPackageOperationRequestDto request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!IsOpaqueId(request.PreviewId)
            || !AgentScopedIdempotencyStore.IsValidKey(request.IdempotencyKey)
            || request.TypedConfirmation is null
            || request.TypedConfirmation.Length > 128)
        {
            throw new AgentPackageRejectedException("package_request_invalid");
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
            throw new AgentPackageRejectedException(reservation.State switch
            {
                AgentIdempotencyReservationState.InProgress => "package_request_in_progress",
                AgentIdempotencyReservationState.Capacity => "package_operation_capacity",
                _ => "package_idempotency_conflict"
            });
        }

        if (!previews.TryGet(principalSid, request.PreviewId, out var preview) || preview is null)
        {
            idempotency.Release(principalSid, IdempotencyScope, request.IdempotencyKey);
            throw new AgentPackageRejectedException("package_preview_invalid");
        }
        if (!string.Equals(request.TypedConfirmation, preview.Confirmation, StringComparison.Ordinal))
        {
            idempotency.Release(principalSid, IdempotencyScope, request.IdempotencyKey);
            throw new AgentPackageRejectedException("package_confirmation_invalid");
        }

        if (preview.Operation != AgentPackageOperationKind.Health)
        {
            if (!IsOpaqueId(request.SnapshotId))
            {
                idempotency.Release(principalSid, IdempotencyScope, request.IdempotencyKey);
                throw new AgentPackageRejectedException("fresh_snapshot_required");
            }

            var verification = await snapshots.ReadAndVerifyAsync(principalSid, request.SnapshotId!, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            if (!verification.Verified)
            {
                idempotency.Release(principalSid, IdempotencyScope, request.IdempotencyKey);
                throw new AgentPackageRejectedException("fresh_snapshot_not_verified");
            }
        }

        if (!previews.TryTake(principalSid, request.PreviewId, out preview) || preview is null)
        {
            idempotency.Release(principalSid, IdempotencyScope, request.IdempotencyKey);
            throw new AgentPackageRejectedException("package_preview_replayed");
        }

        var accepted = operations.Add(principalSid, preview.Operation, clock.GetUtcNow(), correlationId);
        idempotency.Bind(principalSid, IdempotencyScope, request.IdempotencyKey, accepted.OperationId);
        _ = Task.Run(
            () => ExecuteAsync(principalSid, accepted.OperationId, preview, request.SnapshotId, correlationId),
            CancellationToken.None);
        return accepted;
    }

    public bool TryGetOperation(string principalSid, string operationId, out AgentPackageOperationDto? response)
    {
        if (IsOpaqueId(operationId)) return operations.TryGet(principalSid, operationId, out response);
        response = null;
        return false;
    }

    public async Task<(AgentPackageManifest? Manifest, AgentPackageValidationResult Validation)> VerifyForRepairAsync(
        AgentPackageOperationKind operation,
        CancellationToken cancellationToken = default)
    {
        var manifest = await catalog.GetAvailableAsync(operation, cancellationToken).ConfigureAwait(false);
        if (manifest is null) return (null, new(AgentPackageVerificationState.Unknown, ["server_owned_package_unavailable"], "The server-owned repair package is unavailable."));
        return (manifest, await verifier.VerifyAsync(manifest, cancellationToken).ConfigureAwait(false));
    }

    private async Task ExecuteAsync(
        string principalSid,
        string operationId,
        AgentPackagePreviewEntry preview,
        string? snapshotId,
        string correlationId)
    {
        operations.Update(principalSid, operationId, current => current with
        {
            State = AgentPackageOperationStateDto.Staging,
            Outcome = AgentPackageOperationStateDto.Staging,
            ProgressPercent = 20,
            Stage = "staging",
            Detail = "The verified package is being staged inside the fixed Agent boundary."
        });

        AgentPackageExecutionResult result;
        try
        {
            result = await lifecycle.ExecuteAsync(new(
                preview.Operation,
                preview.Manifest,
                snapshotId,
                principalSid,
                correlationId), CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            result = new(AgentPackageOperationState.OutcomeUnknown, false, false, true, "The package lifecycle outcome could not be determined safely.");
        }

        var completed = clock.GetUtcNow();
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
            CompletedAtUtc = completed
        });

        timeline.Record(
            principalSid,
            "AgentPackage",
            result.RecoveryRequired || !result.RollbackSucceeded && result.RollbackAttempted ? FailureSeverity.ActionRequired : FailureSeverity.Informational,
            SafeDetail(result.Detail),
            operationId: AgentPackageOperation.OperationId,
            correlationId: correlationId);
    }

    private static AgentPackageManifestDto ToDto(AgentPackageManifest manifest) =>
        new(
            manifest.PackageId,
            manifest.Version,
            manifest.SupportedOperatingSystem,
            manifest.SupportedRuntime,
            manifest.ServiceDisplayName,
            manifest.ServiceIdentity,
            manifest.ScmName,
            manifest.SignatureAlgorithm,
            manifest.SignerDisplayName,
            manifest.PackageSha256,
            manifest.Files.Select(file => new AgentPackageFileDto(file.LogicalName, file.SizeBytes, file.Sha256, file.Required)).ToArray(),
            manifest.AclRequirements,
            manifest.CertificateRequirements,
            manifest.PreviousVersion,
            manifest.RollbackAvailable);

    private static string[] EffectsFor(AgentPackageOperationKind operation) => operation switch
    {
        AgentPackageOperationKind.Install => ["stage the verified Agent package", "register the owned Agent service only", "verify health before reporting success"],
        AgentPackageOperationKind.Upgrade => ["retain the prior Agent version for rollback", "activate the verified package through the typed platform", "verify health before reporting success"],
        AgentPackageOperationKind.Uninstall => ["remove only the Agent-owned package boundary", "preserve recovery truth"],
        AgentPackageOperationKind.Rollback => ["restore the server-owned prior Agent version", "verify health before reporting success"],
        AgentPackageOperationKind.Health => ["verify the typed Agent package and health boundary"],
        _ => ["execute only the typed Agent package boundary"]
    };

    private static string ConfirmationFor(AgentPackageOperationKind operation) => operation switch
    {
        AgentPackageOperationKind.Install => "INSTALL VERIFIED AGENT PACKAGE",
        AgentPackageOperationKind.Upgrade => "UPGRADE VERIFIED AGENT PACKAGE",
        AgentPackageOperationKind.Uninstall => "UNINSTALL AGENT PACKAGE",
        AgentPackageOperationKind.Rollback => "ROLLBACK AGENT PACKAGE",
        AgentPackageOperationKind.Repair => "REPAIR AGENT PACKAGE",
        _ => "VERIFY AGENT PACKAGE HEALTH"
    };

    private static AgentPackageOperationKindDto Map(AgentPackageOperationKind value) =>
        Enum.TryParse<AgentPackageOperationKindDto>(value.ToString(), out var mapped) ? mapped : AgentPackageOperationKindDto.Repair;

    private static AgentPackageVerificationStateDto Map(AgentPackageVerificationState value) =>
        Enum.TryParse<AgentPackageVerificationStateDto>(value.ToString(), out var mapped) ? mapped : AgentPackageVerificationStateDto.Unknown;

    private static AgentPackageOperationStateDto Map(AgentPackageOperationState value) =>
        Enum.TryParse<AgentPackageOperationStateDto>(value.ToString(), out var mapped) ? mapped : AgentPackageOperationStateDto.OutcomeUnknown;

    private static string SafeDetail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "The package operation completed without additional detail." : value.Replace('\r', ' ').Replace('\n', ' ')[..Math.Min(value.Length, 256)];

    private static bool IsOpaqueId(string? value) => value is { Length: 32 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public sealed class AgentPackageRejectedException(string code) : InvalidOperationException(code);
