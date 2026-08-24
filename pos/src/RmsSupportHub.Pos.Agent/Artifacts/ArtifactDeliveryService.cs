using System.Security.Principal;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Contracts.V1.Artifacts;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Artifacts;

/// <summary>
/// Shared local delivery path for database backups and Support Bundles. Agent-owned source bytes
/// are resolved from a principal-scoped capability; destination filesystem work is delegated to
/// the authenticated WPF caller's token by the dedicated export-only authority seam.
/// </summary>
public sealed class ArtifactDeliveryService(
    ArtifactCatalog artifacts,
    IRmsDatabaseBackupStorage backups,
    IBackupFileSystem fileSystem,
    LocalArtifactDestinationPolicy destinationPolicy,
    IArtifactDestinationAuthority destinationAuthority,
    BoundedKeyedMutationCoordinator destinationCoordinator,
    IAgentAuditSink audit,
    Support.SupportBundleOptions supportBundleOptions,
    RmsDatabaseStorageOptions databaseStorageOptions,
    TimeProvider timeProvider)
{
    public async Task<LocalIpcArtifactExportResultDto> ExportAsync(
        InvocationContext context,
        string principalSid,
        LocalIpcArtifactExportRequestDto request,
        WindowsIdentity? callerIdentity,
        CancellationToken cancellationToken = default)
    {
        var decision = AgentOperationAuthorization.Authorize(
            context,
            AgentOperationRisk.AdministratorOnlyMutation);
        if (!decision.Allowed
            || !IsSafeSid(principalSid)
            || !string.Equals(principalSid, context.AuthenticatedCaller, StringComparison.Ordinal)
            || !HasExportAuthority(principalSid, callerIdentity))
        {
            return Failure(request?.ArtifactId, LocalIpcArtifactExportState.Unauthorized, "unauthorized");
        }

        var exportIdentity = callerIdentity!;

        if (!TryValidateRequest(request, out var expectedExtension))
        {
            return Failure(request?.ArtifactId, LocalIpcArtifactExportState.DestinationRejected, "invalid_request");
        }

        if (!destinationPolicy.TryValidate(principalSid, request.DestinationPath, expectedExtension, out var destination))
        {
            return Failure(request.ArtifactId, LocalIpcArtifactExportState.DestinationRejected, "destination_rejected");
        }

        if (!destinationCoordinator.TryEnter(destination.FullPath, out var lease))
        {
            return Failure(request.ArtifactId, LocalIpcArtifactExportState.Failed, "operation_in_progress");
        }

        using (lease)
        {
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

            // This durable accepted record is required before any destination-side write. A false
            // return means the sink could not persist the event, even if it retained a fallback.
            if (!TryRecordAudit(context, request, destination, "accepted", null))
            {
                return Failure(request.ArtifactId, LocalIpcArtifactExportState.Failed, "audit_unavailable");
            }

            DestinationWriteResult writeResult;
            try
            {
                await using var input = await source.OpenReadAsync(cancellationToken).ConfigureAwait(false);
                writeResult = await destinationAuthority.RunAsync(
                    exportIdentity,
                    () => WriteDestinationAsync(
                        principalSid,
                        destination,
                        request.OverwriteConfirmed,
                        source,
                        input,
                        cancellationToken)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                writeResult = DestinationWriteResult.Failure(LocalIpcArtifactExportState.Cancelled, "cancelled");
            }
            catch
            {
                writeResult = DestinationWriteResult.Failure(LocalIpcArtifactExportState.Failed, "export_failed");
            }

            if (writeResult.State != LocalIpcArtifactExportState.Succeeded)
            {
                var auditAvailable = TryRecordAudit(
                    context,
                    request,
                    destination,
                    OutcomeFor(writeResult.State),
                    writeResult.ErrorCode);
                return auditAvailable
                    ? Failure(request.ArtifactId, writeResult.State, writeResult.ErrorCode)
                    : Failure(request.ArtifactId, LocalIpcArtifactExportState.Failed, "audit_unavailable");
            }

            if (!TryRecordAudit(context, request, destination, "completed", null))
            {
                // A completed output without a durable completed audit is not a success. The
                // rollback descriptor is retained only for this one compensation attempt.
                try
                {
                    await destinationAuthority.RunAsync(
                        exportIdentity,
                        () => CompensateAsync(destination, source, writeResult)).ConfigureAwait(false);
                }
                catch
                {
                    // The safe result remains audit_unavailable; the destination operation never
                    // becomes a claimed success when compensation itself is unavailable.
                }

                return Failure(request.ArtifactId, LocalIpcArtifactExportState.Failed, "audit_unavailable");
            }

            try
            {
                await destinationAuthority.RunAsync(
                    exportIdentity,
                    () => CleanupRollbackAsync(writeResult.RollbackPath)).ConfigureAwait(false);
            }
            catch
            {
                // The completed audit is durable and the output is valid. A stale rollback temp is
                // not exposed through the protocol. Cleanup is attempted for this operation;
                // process termination can leave caller-owned temporary/rollback files requiring
                // later hygiene.
            }

            return new(
                LocalIpcArtifactExportState.Succeeded,
                request.ArtifactId,
                source.DisplayName,
                source.SizeBytes,
                source.Checksum,
                destination.Category,
                null);
        }
    }

    private async Task<DestinationWriteResult> WriteDestinationAsync(
        string principalSid,
        LocalArtifactDestination destination,
        bool overwriteConfirmed,
        ResolvedArtifact source,
        Stream input,
        CancellationToken cancellationToken)
    {
        if (!destinationPolicy.IsStillSafe(principalSid, destination))
        {
            return DestinationWriteResult.Failure(LocalIpcArtifactExportState.DestinationRejected, "destination_rejected");
        }

        string? rollbackPath = null;
        var originalMoved = false;
        var committed = false;
        try
        {
            var destinationExists = fileSystem.FileExists(destination.FullPath);
            if (destinationExists && !overwriteConfirmed)
            {
                return DestinationWriteResult.Failure(LocalIpcArtifactExportState.DestinationExists, "destination_exists");
            }

            if (destinationExists)
            {
                rollbackPath = Path.Combine(
                    destination.ParentDirectory,
                    $".{destination.FileName}.{Guid.NewGuid():N}.rollback");
                if (!destinationPolicy.IsStillSafe(principalSid, destination)
                    || !IsWithinParent(destination.ParentDirectory, rollbackPath))
                {
                    return DestinationWriteResult.Failure(LocalIpcArtifactExportState.DestinationRejected, "destination_rejected");
                }

                await fileSystem.MoveFileAsync(destination.FullPath, rollbackPath, overwrite: false, CancellationToken.None).ConfigureAwait(false);
                originalMoved = true;
            }

            await using (var output = await fileSystem.CreateFileAsync(destination.TemporaryPath, cancellationToken).ConfigureAwait(false))
            {
                await CopyBoundedAsync(input, output, source.MaximumBytes, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var size = fileSystem.GetFileLength(destination.TemporaryPath);
            var checksum = await fileSystem.ComputeSha256Async(destination.TemporaryPath, cancellationToken).ConfigureAwait(false);
            if (size != source.SizeBytes || !string.Equals(checksum, source.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                return DestinationWriteResult.Failure(
                    LocalIpcArtifactExportState.ChecksumMismatch,
                    "artifact_checksum_mismatch",
                    rollbackPath,
                    originalMoved);
            }

            if (!destinationPolicy.IsStillSafe(principalSid, destination))
            {
                return DestinationWriteResult.Failure(
                    LocalIpcArtifactExportState.DestinationRejected,
                    "destination_rejected",
                    rollbackPath,
                    originalMoved);
            }

            await fileSystem.MoveFileAsync(destination.TemporaryPath, destination.FullPath, overwrite: false, cancellationToken).ConfigureAwait(false);
            committed = true;
            return DestinationWriteResult.Success(rollbackPath, originalMoved);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return DestinationWriteResult.Failure(LocalIpcArtifactExportState.Cancelled, "cancelled", rollbackPath, originalMoved);
        }
        catch
        {
            return DestinationWriteResult.Failure(LocalIpcArtifactExportState.Failed, "export_failed", rollbackPath, originalMoved);
        }
        finally
        {
            if (!committed)
            {
                await DeleteIfPresentAsync(destination.TemporaryPath).ConfigureAwait(false);
                if (originalMoved && rollbackPath is not null)
                {
                    await RestoreRollbackAsync(destination.FullPath, rollbackPath).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task CompensateAsync(
        LocalArtifactDestination destination,
        ResolvedArtifact source,
        DestinationWriteResult writeResult)
    {
        if (!destinationPolicy.IsStillSafe(source.PrincipalSid, destination))
        {
            return;
        }

        if (writeResult.OriginalMoved && writeResult.RollbackPath is not null)
        {
            if (fileSystem.FileExists(destination.FullPath)
                && !fileSystem.IsReparsePoint(destination.FullPath)
                && fileSystem.GetFileLength(destination.FullPath) == source.SizeBytes
                && string.Equals(
                    await fileSystem.ComputeSha256Async(destination.FullPath, CancellationToken.None).ConfigureAwait(false),
                    source.Checksum,
                    StringComparison.OrdinalIgnoreCase))
            {
                await fileSystem.DeleteFileAsync(destination.FullPath, CancellationToken.None).ConfigureAwait(false);
            }

            await RestoreRollbackAsync(destination.FullPath, writeResult.RollbackPath).ConfigureAwait(false);
            return;
        }

        if (fileSystem.FileExists(destination.FullPath)
            && !fileSystem.IsReparsePoint(destination.FullPath)
            && fileSystem.GetFileLength(destination.FullPath) == source.SizeBytes
            && string.Equals(
                await fileSystem.ComputeSha256Async(destination.FullPath, CancellationToken.None).ConfigureAwait(false),
                source.Checksum,
                StringComparison.OrdinalIgnoreCase))
        {
            await fileSystem.DeleteFileAsync(destination.FullPath, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task RestoreRollbackAsync(string destinationPath, string rollbackPath)
    {
        try
        {
            if (!fileSystem.FileExists(destinationPath))
            {
                await fileSystem.MoveFileAsync(rollbackPath, destinationPath, overwrite: false, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch
        {
            // Preserve the rollback file rather than deleting the only known previous output.
        }
    }

    private async Task CleanupRollbackAsync(string? rollbackPath)
    {
        if (!string.IsNullOrWhiteSpace(rollbackPath))
        {
            await DeleteIfPresentAsync(rollbackPath).ConfigureAwait(false);
        }
    }

    private async Task DeleteIfPresentAsync(string path)
    {
        try
        {
            await fileSystem.DeleteFileAsync(path, CancellationToken.None).ConfigureAwait(false);
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }

    private static async Task CopyBoundedAsync(
        Stream input,
        Stream output,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[128 * 1024];
        long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) return;
            total = checked(total + read);
            if (total > maximumBytes)
            {
                throw new ArtifactSizeLimitException();
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
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
                principalSid,
                async token => await artifacts.OpenReadAsync(principalSid, request.ArtifactId, token).ConfigureAwait(false)));
        }

        if (request.DatabaseTarget is not { } target
            || !TryResolve(target, out var database))
        {
            return SourceResolution.Failure(LocalIpcArtifactExportState.ArtifactNotFound, "artifact_not_found");
        }

        var backup = await backups.ListInventoryAsync(database, principalSid, cancellationToken).ConfigureAwait(false);
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
                principalSid,
                async token => await fileSystem.OpenReadAsync(entry.ServerPath, token).ConfigureAwait(false))),
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
                Source = $"LocalWpf-{destination.Category}"
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

        if (request.ArtifactKind == LocalIpcArtifactKind.DatabaseBackup)
        {
            expectedExtension = ".bak";
            return request.DatabaseTarget is { } target && Enum.IsDefined(target) && TryResolve(target, out _);
        }

        if (request.ArtifactKind == LocalIpcArtifactKind.SupportBundle)
        {
            expectedExtension = ".zip";
            return request.DatabaseTarget is null;
        }

        return false;
    }

    private static string OutcomeFor(LocalIpcArtifactExportState state) => state switch
    {
        LocalIpcArtifactExportState.Cancelled => "cancelled",
        LocalIpcArtifactExportState.ChecksumMismatch => "checksum_mismatch",
        LocalIpcArtifactExportState.DestinationExists or LocalIpcArtifactExportState.DestinationRejected => "destination_rejected",
        _ => "failed"
    };

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

    internal static bool HasExportAuthority(string principalSid, WindowsIdentity? callerIdentity)
    {
        try
        {
            return callerIdentity is not null
                && callerIdentity.User?.Value is { } callerSid
                && string.Equals(callerSid, principalSid, StringComparison.Ordinal)
                && callerIdentity.ImpersonationLevel == TokenImpersonationLevel.Impersonation;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidChecksum(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool IsWithinParent(string parent, string candidate) =>
        string.Equals(parent, Path.GetDirectoryName(candidate), StringComparison.OrdinalIgnoreCase);

    private sealed record ResolvedArtifact(
        string DisplayName,
        long SizeBytes,
        string Checksum,
        DateTimeOffset? ExpiresAtUtc,
        long MaximumBytes,
        string PrincipalSid,
        Func<CancellationToken, Task<Stream?>> OpenRead)
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

    private sealed record DestinationWriteResult(
        LocalIpcArtifactExportState State,
        string ErrorCode,
        string? RollbackPath = null,
        bool OriginalMoved = false)
    {
        public static DestinationWriteResult Success(string? rollbackPath, bool originalMoved) =>
            new(LocalIpcArtifactExportState.Succeeded, string.Empty, rollbackPath, originalMoved);

        public static DestinationWriteResult Failure(
            LocalIpcArtifactExportState state,
            string code,
            string? rollbackPath = null,
            bool originalMoved = false) =>
            new(state, code, rollbackPath, originalMoved);
    }

    private sealed class ArtifactSizeLimitException : InvalidOperationException;
}
