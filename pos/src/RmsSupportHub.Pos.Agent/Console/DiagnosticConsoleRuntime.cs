using System.Text;
using RmsSupportHub.Pos.Agent.Artifacts;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Runtime;
using RmsSupportHub.Pos.Contracts.V1.Console;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Configuration;
using RmsSupportHub.Pos.Infrastructure.Diagnostics;

namespace RmsSupportHub.Pos.Agent.Console;

public static class DiagnosticConsoleOperation
{
    public const string OperationId = "diagnostic-console.run.start";
    public const string HttpMethod = "POST";
    public const string HttpPath = "/api/v1/diagnostic-console/runs";

    public static MutationOperationDescriptor Descriptor { get; } =
        new(OperationId, HttpMethod, HttpPath, MutationTargetKind.None);
}

public sealed class DiagnosticConsolePreviewStore(
    RuntimeRetentionPolicy retention,
    TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public string? Add(string principalSid, DiagnosticConsoleTarget target, DiagnosticConsoleLaunchSpec spec, string confirmation, DateTimeOffset expiresAtUtc)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.Count >= retention.MaxCompletedOperations) return null;
            var id = Guid.NewGuid().ToString("N");
            entries[id] = new(principalSid, target, spec, confirmation, expiresAtUtc);
            return id;
        }
    }

    public bool TryTake(string principalSid, string previewId, out DiagnosticConsolePreviewEntry? entry)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.Remove(previewId, out var found)
                && string.Equals(found.PrincipalSid, principalSid, StringComparison.Ordinal)
                && clock.GetUtcNow() < found.ExpiresAtUtc)
            {
                entry = new(found.PrincipalSid, found.Target, found.Spec, found.Confirmation, found.ExpiresAtUtc);
                return true;
            }

            entry = null;
            return false;
        }
    }

    public bool TryGet(string principalSid, string previewId, out DiagnosticConsolePreviewEntry? entry)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.TryGetValue(previewId, out var found)
                && string.Equals(found.PrincipalSid, principalSid, StringComparison.Ordinal)
                && clock.GetUtcNow() < found.ExpiresAtUtc)
            {
                entry = new(found.PrincipalSid, found.Target, found.Spec, found.Confirmation, found.ExpiresAtUtc);
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
        DiagnosticConsoleTarget Target,
        DiagnosticConsoleLaunchSpec Spec,
        string Confirmation,
        DateTimeOffset ExpiresAtUtc);
}

public sealed record DiagnosticConsolePreviewEntry(
    string PrincipalSid,
    DiagnosticConsoleTarget Target,
    DiagnosticConsoleLaunchSpec Spec,
    string Confirmation,
    DateTimeOffset ExpiresAtUtc);

public sealed class DiagnosticConsoleRunStore(
    RuntimeRetentionPolicy retention,
    TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public DiagnosticConsoleRunDto Add(string principalSid, DiagnosticConsoleTarget target, DateTimeOffset startedAtUtc, string correlationId)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.Count >= retention.MaxCompletedOperations)
            {
                throw new InvalidOperationException("The diagnostic operation retention limit has been reached.");
            }

            var operationId = Guid.NewGuid().ToString("N");
            var response = new DiagnosticConsoleRunDto(
                operationId,
                Map(target),
                DiagnosticConsoleRunStateDto.Accepted,
                DiagnosticConsoleRunStateDto.Accepted,
                0,
                "accepted",
                "The typed diagnostic run was accepted by the Agent.",
                startedAtUtc,
                null,
                null,
                null,
                correlationId);
            entries[operationId] = new(principalSid, response);
            return response;
        }
    }

    public bool TryGet(string principalSid, string operationId, out DiagnosticConsoleRunDto? response)
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

    public bool Update(string principalSid, string operationId, Func<DiagnosticConsoleRunDto, DiagnosticConsoleRunDto> update)
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
                && now - completed >= retention.CompletedOperationLifetime)
            {
                entries.Remove(pair.Key);
            }
        }
    }

    private sealed record Entry(string PrincipalSid, DiagnosticConsoleRunDto Response);

    private static DiagnosticConsoleTargetDto Map(DiagnosticConsoleTarget target) => target switch
    {
        DiagnosticConsoleTarget.BranchServerApi => DiagnosticConsoleTargetDto.BranchServerApi,
        DiagnosticConsoleTarget.CashierServerApi => DiagnosticConsoleTargetDto.CashierServerApi,
        DiagnosticConsoleTarget.ServiceManager => DiagnosticConsoleTargetDto.ServiceManager,
        _ => DiagnosticConsoleTargetDto.CashierUi
    };
}

public sealed class DiagnosticConsoleRuntime(
    IRmsInstallationDiscovery discovery,
    IDiagnosticConsoleManifest manifest,
    IDiagnosticConsolePolicy policy,
    IConstrainedDiagnosticProcessRunner runner,
    IDiagnosticConsoleOutputRedactor redactor,
    IBackupFileSystem fileSystem,
    ArtifactCatalog artifacts,
    DiagnosticConsoleOptions options,
    DiagnosticConsolePreviewStore previews,
    DiagnosticConsoleRunStore runs,
    AgentScopedIdempotencyStore idempotency,
    IncidentTimelineService timeline,
    TimeProvider clock)
{
    private const string IdempotencyScope = "diagnostic-console.run";
    private const string PreviewIdempotencyScope = "diagnostic-console.preview";

    public async Task<DiagnosticConsolePreviewDto> PreviewAsync(
        string principalSid,
        DiagnosticConsoleTarget target,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!AgentScopedIdempotencyStore.IsValidKey(idempotencyKey))
        {
            throw new DiagnosticConsoleRejectedException("diagnostic_preview_idempotency_invalid");
        }

        var reservation = idempotency.TryReserve(principalSid, PreviewIdempotencyScope, idempotencyKey, target.ToString());
        if (reservation.State == AgentIdempotencyReservationState.Completed
            && reservation.OperationId is not null
            && previews.TryGet(principalSid, reservation.OperationId, out var existing)
            && existing is not null)
        {
            return ToPreview(reservation.OperationId, existing);
        }
        if (reservation.State != AgentIdempotencyReservationState.Reserved)
        {
            throw new DiagnosticConsoleRejectedException(reservation.State switch
            {
                AgentIdempotencyReservationState.InProgress => "diagnostic_preview_in_progress",
                AgentIdempotencyReservationState.Capacity => "diagnostic_preview_capacity",
                _ => "diagnostic_preview_idempotency_conflict"
            });
        }

        try
        {
            var now = clock.GetUtcNow();
            var expires = now.AddMinutes(5);
            var displayName = target.ToString();
            if (manifest.TryGet(target, out var definition)) displayName = definition.DisplayName;

            var installation = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
            if (!policy.TryCreateLaunchSpec(target, installation, out definition!, out var spec, out var failureCode)
                || spec is null)
            {
                idempotency.Release(principalSid, PreviewIdempotencyScope, idempotencyKey);
                return new(
                    string.Empty,
                    Map(target),
                    false,
                    displayName,
                    DiagnosticConsoleRunStateDto.NotAttempted,
                    definition?.MaxWallTime.TotalSeconds is { } seconds ? (int)seconds : 30,
                    definition?.MaxOutputBytes ?? 64 * 1024,
                    definition?.MaxOutputLines ?? 512,
                    ["fixed Agent manifest", "fixed RMS installation discovery", "bounded stdout and stderr", "separate redacted artifacts"],
                    [failureCode ?? "console_precondition_unknown"],
                    ConfirmationFor(displayName),
                    expires);
            }

            var confirmation = ConfirmationFor(definition.DisplayName);
            var previewId = previews.Add(principalSid, target, spec, confirmation, expires);
            if (previewId is null)
            {
                idempotency.Release(principalSid, PreviewIdempotencyScope, idempotencyKey);
                return new(
                    string.Empty,
                    Map(target),
                    false,
                    definition.DisplayName,
                    DiagnosticConsoleRunStateDto.NotAttempted,
                    (int)definition.MaxWallTime.TotalSeconds,
                    definition.MaxOutputBytes,
                    definition.MaxOutputLines,
                    [],
                    ["console_preview_capacity"],
                    confirmation,
                    expires);
            }

            idempotency.Bind(principalSid, PreviewIdempotencyScope, idempotencyKey, previewId);
            return new(
                previewId,
                Map(target),
                true,
                definition.DisplayName,
                DiagnosticConsoleRunStateDto.NotAttempted,
                (int)definition.MaxWallTime.TotalSeconds,
                definition.MaxOutputBytes,
                definition.MaxOutputLines,
                ["fixed Agent executable manifest", "fixed working directory", "fixed diagnostic arguments", "empty child environment", "bounded stdout and stderr", "redacted opaque artifacts"],
                [],
                confirmation,
                expires);
        }
        catch
        {
            idempotency.Release(principalSid, PreviewIdempotencyScope, idempotencyKey);
            throw;
        }
    }

    private DiagnosticConsolePreviewDto ToPreview(string previewId, DiagnosticConsolePreviewEntry entry)
    {
        var definition = manifest.TryGet(entry.Target, out var found) ? found : null;
        return new(
            previewId,
            Map(entry.Target),
            true,
            definition?.DisplayName ?? entry.Target.ToString(),
            DiagnosticConsoleRunStateDto.NotAttempted,
            definition is null ? 30 : (int)definition.MaxWallTime.TotalSeconds,
            definition?.MaxOutputBytes ?? 64 * 1024,
            definition?.MaxOutputLines ?? 512,
            ["fixed Agent executable manifest", "fixed working directory", "fixed diagnostic arguments", "empty child environment", "bounded stdout and stderr", "redacted opaque artifacts"],
            [],
            entry.Confirmation,
            entry.ExpiresAtUtc);
    }

    public async Task<DiagnosticConsoleRunDto> StartAsync(
        string principalSid,
        DiagnosticConsoleStartRequestDto request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!IsOpaqueId(request.PreviewId)
            || !AgentScopedIdempotencyStore.IsValidKey(request.IdempotencyKey)
            || request.TypedConfirmation is null
            || request.TypedConfirmation.Length > 128)
        {
            throw new DiagnosticConsoleRejectedException("diagnostic_request_invalid");
        }

        var reservation = idempotency.TryReserve(
            principalSid,
            IdempotencyScope,
            request.IdempotencyKey,
            request.PreviewId + "|" + request.TypedConfirmation);
        if (reservation.State == AgentIdempotencyReservationState.Completed
            && reservation.OperationId is not null
            && runs.TryGet(principalSid, reservation.OperationId, out var existing)
            && existing is not null)
        {
            return existing;
        }

        if (reservation.State != AgentIdempotencyReservationState.Reserved)
        {
            throw new DiagnosticConsoleRejectedException(reservation.State switch
            {
                AgentIdempotencyReservationState.InProgress => "diagnostic_request_in_progress",
                AgentIdempotencyReservationState.Capacity => "diagnostic_operation_capacity",
                _ => "diagnostic_idempotency_conflict"
            });
        }

        if (!previews.TryTake(principalSid, request.PreviewId, out var preview) || preview is null)
        {
            idempotency.Release(principalSid, IdempotencyScope, request.IdempotencyKey);
            throw new DiagnosticConsoleRejectedException("diagnostic_preview_invalid");
        }

        if (!string.Equals(request.TypedConfirmation, preview.Confirmation, StringComparison.Ordinal))
        {
            idempotency.Release(principalSid, IdempotencyScope, request.IdempotencyKey);
            throw new DiagnosticConsoleRejectedException("diagnostic_confirmation_invalid");
        }

        var startedAt = clock.GetUtcNow();
        var accepted = runs.Add(principalSid, preview.Target, startedAt, correlationId);
        idempotency.Bind(principalSid, IdempotencyScope, request.IdempotencyKey, accepted.OperationId);
        _ = Task.Run(
            () => ExecuteAsync(principalSid, accepted.OperationId, preview, correlationId, cancellationToken: CancellationToken.None),
            CancellationToken.None);
        return accepted;
    }

    public bool TryGet(string principalSid, string operationId, out DiagnosticConsoleRunDto? response)
    {
        if (IsOpaqueId(operationId)) return runs.TryGet(principalSid, operationId, out response);
        response = null;
        return false;
    }

    private async Task ExecuteAsync(
        string principalSid,
        string operationId,
        DiagnosticConsolePreviewEntry preview,
        string correlationId,
        CancellationToken cancellationToken)
    {
        runs.Update(principalSid, operationId, current => current with
        {
            State = DiagnosticConsoleRunStateDto.Running,
            Outcome = DiagnosticConsoleRunStateDto.Running,
            ProgressPercent = 10,
            Stage = "running",
            Detail = "The fixed diagnostic process is running within the Agent bounds."
        });

        DiagnosticConsoleProcessResult processResult;
        try
        {
            var definition = manifest.TryGet(preview.Target, out var found)
                ? found
                : null;
            if (definition is null)
            {
                processResult = new(DiagnosticConsoleProcessState.Unknown, null, string.Empty, string.Empty, 0, 0, 0, 0, false, false, "The diagnostic manifest was unavailable.");
            }
            else
            {
                processResult = await runner.RunAsync(
                    preview.Spec,
                    definition.MaxWallTime,
                    definition.MaxOutputBytes,
                    definition.MaxOutputLines,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            processResult = new(DiagnosticConsoleProcessState.Cancelled, null, string.Empty, string.Empty, 0, 0, 0, 0, false, false, "The diagnostic run was cancelled.");
        }
        catch
        {
            processResult = new(DiagnosticConsoleProcessState.Unknown, null, string.Empty, string.Empty, 0, 0, 0, 0, false, false, "The diagnostic outcome could not be determined safely.");
        }

        string stdout;
        string stderr;
        try
        {
            // The runner has already bounded the in-memory streams. No raw child output reaches
            // an artifact, timeline, or response if the sanitizer itself fails.
            stdout = redactor.Redact(Bounded(processResult.StandardOutput));
            stderr = redactor.Redact(Bounded(processResult.StandardError));
        }
        catch
        {
            stdout = "[diagnostic output quarantined: redaction failed]";
            stderr = "[diagnostic output quarantined: redaction failed]";
            processResult = processResult with { OutputTruncated = true, Detail = "The diagnostic output was quarantined because it could not be redacted safely." };
        }
        var stdoutArtifact = await WriteArtifactAsync(principalSid, "diagnostic-stdout.txt", stdout, cancellationToken).ConfigureAwait(false);
        var stderrArtifact = await WriteArtifactAsync(principalSid, "diagnostic-stderr.txt", stderr, cancellationToken).ConfigureAwait(false);
        var outcome = MapOutcome(processResult);
        var completedAt = clock.GetUtcNow();
        var result = new DiagnosticConsoleResultDto(
            stdoutArtifact,
            stderrArtifact,
            Encoding.UTF8.GetByteCount(stdout),
            Encoding.UTF8.GetByteCount(stderr),
            CountLines(stdout),
            CountLines(stderr),
            processResult.OutputTruncated,
            true,
            processResult.ExitCode,
            SafeDetail(processResult.Detail));
        runs.Update(principalSid, operationId, current => current with
        {
            State = outcome,
            Outcome = outcome,
            ProgressPercent = 100,
            Stage = "completed",
            Detail = SafeDetail(processResult.Detail),
            CompletedAtUtc = completedAt,
            Result = result,
            ErrorCode = outcome is DiagnosticConsoleRunStateDto.Succeeded or DiagnosticConsoleRunStateDto.Partial ? null : outcome.ToString().ToLowerInvariant()
        });

        timeline.Record(
            principalSid,
            "DiagnosticConsole",
            outcome is DiagnosticConsoleRunStateDto.Succeeded or DiagnosticConsoleRunStateDto.Partial ? FailureSeverity.Informational : FailureSeverity.Warning,
            SafeDetail(processResult.Detail),
            operationId: DiagnosticConsoleOperation.OperationId,
            correlationId: correlationId);
    }

    private async Task<string?> WriteArtifactAsync(
        string principalSid,
        string displayName,
        string content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(content)) return null;
        try
        {
            options.Validate();
            // Redaction is complete before this call, and the output boundary is reconciled before
            // any artifact file is opened. A hostile owner or reparse point therefore blocks the
            // write instead of turning diagnostic output into an uncontrolled file primitive.
            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(options.OutputRoot);
            await fileSystem.EnsureDirectoryAsync(options.OutputRoot, cancellationToken).ConfigureAwait(false);
            var path = Path.Combine(options.OutputRoot, Guid.NewGuid().ToString("N") + ".txt");
            await using (var stream = await fileSystem.CreateFileAsync(path, cancellationToken).ConfigureAwait(false))
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var size = fileSystem.GetFileLength(path);
            var checksum = await fileSystem.ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
            return artifacts.Register(principalSid, displayName, path, size, checksum, clock.GetUtcNow()).ArtifactId;
        }
        catch
        {
            return null;
        }
    }

    private static DiagnosticConsoleRunStateDto MapOutcome(DiagnosticConsoleProcessResult result) =>
        result.State switch
        {
            DiagnosticConsoleProcessState.Succeeded when result.OutputTruncated => DiagnosticConsoleRunStateDto.Partial,
            DiagnosticConsoleProcessState.Succeeded => DiagnosticConsoleRunStateDto.Succeeded,
            DiagnosticConsoleProcessState.Failed => DiagnosticConsoleRunStateDto.Failed,
            DiagnosticConsoleProcessState.TimedOut => DiagnosticConsoleRunStateDto.TimedOut,
            DiagnosticConsoleProcessState.Cancelled => DiagnosticConsoleRunStateDto.Cancelled,
            _ => DiagnosticConsoleRunStateDto.OutcomeUnknown
        };

    private static DiagnosticConsoleTargetDto Map(DiagnosticConsoleTarget target) => target switch
    {
        DiagnosticConsoleTarget.BranchServerApi => DiagnosticConsoleTargetDto.BranchServerApi,
        DiagnosticConsoleTarget.CashierServerApi => DiagnosticConsoleTargetDto.CashierServerApi,
        DiagnosticConsoleTarget.ServiceManager => DiagnosticConsoleTargetDto.ServiceManager,
        _ => DiagnosticConsoleTargetDto.CashierUi
    };

    private static string ConfirmationFor(string displayName) =>
        "RUN " + new string(displayName.ToUpperInvariant().Select(character => char.IsLetterOrDigit(character) ? character : ' ').ToArray()).Replace("  ", " ", StringComparison.Ordinal).Trim() + " DIAGNOSTICS";

    private static string Bounded(string value) => value is null ? string.Empty : value[..Math.Min(value.Length, 256 * 1024)];

    private static int CountLines(string value) => value.Length == 0 ? 0 : value.Count(character => character == '\n') + 1;

    private static string SafeDetail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "The diagnostic run completed without additional detail." : value.Replace('\r', ' ').Replace('\n', ' ')[..Math.Min(value.Length, 256)];

    private static bool IsOpaqueId(string? value) => value is { Length: 32 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public sealed class DiagnosticConsoleRejectedException(string code) : InvalidOperationException(code);
