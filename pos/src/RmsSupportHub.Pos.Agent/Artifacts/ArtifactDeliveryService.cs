using System.Collections.Concurrent;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Contracts.V1.Artifacts;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Artifacts;

/// <summary>
/// Shared local delivery path for database backups and Support Bundles. The source is always
/// resolved from a principal-scoped Agent capability; only the user-selected output path crosses
/// the Local IPC boundary.
/// </summary>
public sealed class ArtifactDeliveryService(
    ArtifactCatalog artifacts,
    IRmsDatabaseBackupStorage backups,
    IBackupFileSystem fileSystem,
    LocalArtifactDestinationPolicy destinationPolicy,
    IAgentAuditSink audit,
    Support.SupportBundleOptions supportBundleOptions,
    RmsDatabaseStorageOptions databaseStorageOptions,
    TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> destinationGates = new(StringComparer.OrdinalIgnoreCase);

    public async Task<LocalIpcArtifactExportResultDto> ExportAsync(
        InvocationContext context,
        string principalSid,
        LocalIpcArtifactExportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var decision = AgentOperationAuthorization.Authorize(
            context,
            AgentOperationRisk.AdministratorOnlyMutation);
        if (!decision.Allowed
            || !IsSafeSid(principalSid)
            || !string.Equals(principalSid, context.AuthenticatedCaller, StringComparison.Ordinal))
        {
            return Failure(request?.ArtifactId, LocalIpcArtifactExportState.Unauthorized, "unauthorized");
        }

        if (!TryValidateRequest(request, out var expectedExtension))
        {
            return Failure(request?.ArtifactId, LocalIpcArtifactExportState.DestinationRejected, "invalid_request");
        }

        if (!destinationPolicy.TryValidate(request.DestinationPath, expectedExtension, out var destination))
        {
            return Failure(request.ArtifactId, LocalIpcArtifactExportState.DestinationRejected, "destination_rejected");
        }

        var gate = destinationGates.GetOrAdd(destination.FullPath, static _ => new SemaphoreSlim(1, 1));
        if (!gate.Wait(0))
        {
            return Failure(request.ArtifactId, LocalIpcArtifactExportState.Failed, "operation_in_progress");
        }

        try
        {
            if (fileSystem.FileExists(destination.FullPath) && !request.OverwriteConfirmed)
            {
                TryRecordAudit(context, request, destination, "destination_exists", "destination_exists");
                return Failure(request.ArtifactId, LocalIpcArtifactExportState.DestinationExists, "destination_exists");
            }

            var resolution = await ResolveSourceAsync(principalSid, request, cancellationToken).ConfigureAwait(false);
            if (resolution.Artifact is null)
            {
                return Failure(
                    request.ArtifactId,
                    resolution.FailureState ?? LocalIpcArtifactExportState.ArtifactNotFound,
                    resolution.FailureCode ?? "artifact_not_found");
            }

            var source = resolution.Artifact;
            if (source.ExpiresAtUtc is { } expiresAtUtc && timeProvider.GetUtcNow() >= expiresAtUtc)
            {
                return Failure(request.ArtifactId, LocalIpcArtifactExportState.ArtifactExpired, "artifact_expired");
            }

            if (!IsValidChecksum(source.Checksum)
                || source.SizeBytes <= 0
                || source.SizeBytes > source.MaximumBytes)
            {
                return Failure(request.ArtifactId, LocalIpcArtifactExportState.ChecksumMismatch, "artifact_checksum_mismatch");
            }

            // The audit is deliberately pre-operation and exactly once. If it cannot be persisted,
            // no destination bytes are written and no success can be reported.
            if (!TryRecordAudit(context, request, destination, "accepted", null))
            {
                return Failure(request.ArtifactId, LocalIpcArtifactExportState.Failed, "audit_unavailable");
            }

            try
            {
                await using var input = await source.OpenReadAsync(cancellationToken).ConfigureAwait(false);
                await using (var output = await fileSystem.CreateFileAsync(destination.TemporaryPath, cancellationToken).ConfigureAwait(false))
                {
                    await input.CopyToAsync(output, 128 * 1024, cancellationToken).ConfigureAwait(false);
                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                var size = fileSystem.GetFileLength(destination.TemporaryPath);
                var checksum = await fileSystem.ComputeSha256Async(destination.TemporaryPath, cancellationToken).ConfigureAwait(false);
                if (size != source.SizeBytes || !string.Equals(checksum, source.Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    return Failure(request.ArtifactId, LocalIpcArtifactExportState.ChecksumMismatch, "artifact_checksum_mismatch");
                }

                await fileSystem.MoveFileAsync(
                    destination.TemporaryPath,
                    destination.FullPath,
                    request.OverwriteConfirmed,
                    cancellationToken).ConfigureAwait(false);

                return new(
                    LocalIpcArtifactExportState.Succeeded,
                    request.ArtifactId,
                    source.DisplayName,
                    source.SizeBytes,
                    source.Checksum,
                    destination.Category,
                    null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Failure(request.ArtifactId, LocalIpcArtifactExportState.Cancelled, "cancelled");
            }
            catch
            {
                return Failure(request.ArtifactId, LocalIpcArtifactExportState.Failed, "export_failed");
            }
            finally
            {
                try
                {
                    await fileSystem.DeleteFileAsync(destination.TemporaryPath, CancellationToken.None).ConfigureAwait(false);
                }
                catch { }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<SourceResolution> ResolveSourceAsync(
        string principalSid,
        LocalIpcArtifactExportRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.ArtifactKind == LocalIpcArtifactKind.SupportBundle)
        {
            if (!artifacts.TryGetIncludingExpired(principalSid, request.ArtifactId, out ArtifactMetadataDto? metadata)
                || metadata is null)
            {
                return SourceResolution.Failure(LocalIpcArtifactExportState.ArtifactNotFound, "artifact_not_found");
            }

            return SourceResolution.Success(new(
                metadata.DisplayName,
                metadata.SizeBytes,
                metadata.Sha256Checksum,
                metadata.ExpiresAtUtc,
                supportBundleOptions.MaximumBundleBytes,
                async token => await artifacts.OpenReadAsync(principalSid, request.ArtifactId, token).ConfigureAwait(false)));
        }

        if (request.DatabaseTarget is not { } target
            || !TryResolve(target, out var database))
        {
            return SourceResolution.Failure(LocalIpcArtifactExportState.ArtifactNotFound, "artifact_not_found");
        }

        var backup = (await backups
            .ListInventoryAsync(database, principalSid, cancellationToken)
            .ConfigureAwait(false));
        var entry = backup.FirstOrDefault(candidate =>
            string.Equals(candidate.ArtifactId, request.ArtifactId, StringComparison.Ordinal));
        if (entry is null)
        {
            return SourceResolution.Failure(LocalIpcArtifactExportState.ArtifactNotFound, "artifact_not_found");
        }

        return entry.Availability switch
        {
            RmsDatabaseBackupAvailability.Expired => SourceResolution.Failure(LocalIpcArtifactExportState.ArtifactExpired, "artifact_expired"),
            RmsDatabaseBackupAvailability.ChecksumMismatch => SourceResolution.Failure(LocalIpcArtifactExportState.ChecksumMismatch, "artifact_checksum_mismatch"),
            RmsDatabaseBackupAvailability.Available => SourceResolution.Success(new(
                entry.DisplayName,
                entry.SizeBytes,
                entry.Sha256Checksum,
                entry.ExpiresAtUtc,
                databaseStorageOptions.MaximumBackupBytes,
                async token => await fileSystem.OpenReadAsync(entry.ServerPath, token).ConfigureAwait(false),
                entry.ServerPath)),
            _ => SourceResolution.Failure(LocalIpcArtifactExportState.ArtifactNotFound, "artifact_not_found")
        };
    }

    private bool TryRecordAudit(
        InvocationContext context,
        LocalIpcArtifactExportRequestDto request,
        LocalArtifactDestination destination,
        string outcome,
        string? failureCode)
    {
        try
        {
            return audit.Record(new AgentAuditEvent(
                timeProvider.GetUtcNow(),
                context.AuthenticatedCaller,
                "artifact.export.local",
                request.ArtifactKind == LocalIpcArtifactKind.DatabaseBackup ? "database-backup" : "support-bundle",
                context.CorrelationId,
                outcome,
                failureCode,
                typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "unavailable",
                null)
            {
                Source = InvocationSource.LocalWpf.ToString()
            });
        }
        catch
        {
            return false;
        }
    }

    private static bool TryValidateRequest(
        LocalIpcArtifactExportRequestDto? request,
        out string expectedExtension)
    {
        expectedExtension = string.Empty;
        if (request is null
            || !IsOpaqueId(request.ArtifactId)
            || !Enum.IsDefined(request.ArtifactKind)
            || string.IsNullOrWhiteSpace(request.DestinationPath)
            || request.DestinationPath.Length > 512)
        {
            return false;
        }

        expectedExtension = request.ArtifactKind == LocalIpcArtifactKind.DatabaseBackup
            ? ".bak"
            : ".zip";
        return request.ArtifactKind != LocalIpcArtifactKind.DatabaseBackup || request.DatabaseTarget is not null;
    }

    private static LocalIpcArtifactExportResultDto Failure(
        string? artifactId,
        LocalIpcArtifactExportState state,
        string code) =>
        new(state, IsOpaqueId(artifactId) ? artifactId! : string.Empty, string.Empty, 0, string.Empty, string.Empty, code);

    private static bool TryResolve(RmsDatabaseTarget target, out RmsDatabaseKind database)
    {
        database = target switch
        {
            RmsDatabaseTarget.Branch => RmsDatabaseKind.Branch,
            RmsDatabaseTarget.Cashier => RmsDatabaseKind.Cashier,
            _ => default
        };
        return target is RmsDatabaseTarget.Branch or RmsDatabaseTarget.Cashier;
    }

    private static bool IsOpaqueId(string? value) =>
        value is { Length: 32 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsSafeSid(string? value) =>
        value is { Length: > 0 and <= 184 }
        && value.StartsWith("S-", StringComparison.OrdinalIgnoreCase)
        && value.All(character => char.IsLetterOrDigit(character) || character == '-');

    private static bool IsValidChecksum(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private sealed record ResolvedArtifact(
        string DisplayName,
        long SizeBytes,
        string Checksum,
        DateTimeOffset? ExpiresAtUtc,
        long MaximumBytes,
        Func<CancellationToken, Task<Stream?>> OpenRead,
        string? ServerPath = null)
    {
        public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken) =>
            await OpenRead(cancellationToken).ConfigureAwait(false)
                ?? throw new FileNotFoundException("The Agent artifact is unavailable.");
    }

    private sealed record SourceResolution(
        ResolvedArtifact? Artifact,
        LocalIpcArtifactExportState? FailureState,
        string? FailureCode)
    {
        public static SourceResolution Success(ResolvedArtifact artifact) => new(artifact, null, null);

        public static SourceResolution Failure(LocalIpcArtifactExportState state, string code) => new(null, state, code);
    }
}
