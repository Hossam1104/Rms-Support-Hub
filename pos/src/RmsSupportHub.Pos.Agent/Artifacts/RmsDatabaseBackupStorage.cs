using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Artifacts;

/// <summary>
/// Device-local capability layer for RMS database backup artifacts. The storage layer accepts only
/// files allocated beneath the fixed Agent-owned backup root and keeps the server path as an
/// internal capability -- the browser receives only an opaque artifact ID and sanitized metadata.
/// Durability, revalidation, and retention live in <see cref="RmsDatabaseBackupCatalog"/>.
/// </summary>
public sealed class RmsDatabaseBackupStorage : IRmsDatabaseBackupStorage
{
    private readonly IBackupFileSystem fileSystem;
    private readonly RmsDatabaseBackupCatalog catalog;
    private readonly RmsDatabaseStorageOptions options;
    private readonly TimeProvider timeProvider;

    public RmsDatabaseBackupStorage(
        IBackupFileSystem fileSystem,
        RmsDatabaseBackupCatalog catalog,
        RmsDatabaseStorageOptions options,
        TimeProvider? timeProvider = null) : this(fileSystem, catalog, options, timeProvider, validate: true)
    {
    }

    private RmsDatabaseBackupStorage(
        IBackupFileSystem fileSystem,
        RmsDatabaseBackupCatalog catalog,
        RmsDatabaseStorageOptions options,
        TimeProvider? timeProvider,
        bool validate)
    {
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        if (validate)
        {
            this.options.Validate();
        }
    }

    public async Task<RmsDatabaseBackupAllocation> AllocateAsync(
        RmsDatabaseKind database,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var definition = RmsDatabaseCatalog.For(database);
        await fileSystem.EnsureDirectoryAsync(options.BackupRootPath, cancellationToken).ConfigureAwait(false);
        EnsureSafeExistingDirectory(options.BackupRootPath);

        var timestamp = createdAtUtc.ToUniversalTime().ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var displayName = $"{definition.DatabaseName}_{timestamp}_{Guid.NewGuid():N}.bak";
        var path = Path.GetFullPath(Path.Combine(options.BackupRootPath, displayName));
        if (!BackupPathSafety.IsWithinRoot(options.BackupRootPath, path)
            || !string.Equals(Path.GetExtension(path), ".bak", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The Agent-owned backup destination is invalid.");
        }

        return new(path, displayName, createdAtUtc);
    }

    public async Task<RmsApprovedDatabaseBackup?> RegisterAsync(
        RmsDatabaseKind database,
        RmsDatabaseBackupAllocation allocation,
        CancellationToken cancellationToken = default,
        string? principalSid = null)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        var path = Path.GetFullPath(allocation.ServerPath);
        if (!BackupPathSafety.IsSafePath(fileSystem, options.BackupRootPath, path)
            || !string.Equals(Path.GetExtension(path), ".bak", StringComparison.OrdinalIgnoreCase)
            || !fileSystem.FileExists(path)
            || fileSystem.IsReparsePoint(path))
        {
            return null;
        }

        long size;
        string checksum;
        try
        {
            size = fileSystem.GetFileLength(path);
            if (size <= 0 || size > options.MaximumBackupBytes)
            {
                try { await fileSystem.DeleteFileAsync(path, CancellationToken.None).ConfigureAwait(false); } catch { }
                return null;
            }

            checksum = await fileSystem.ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }

        var entry = await catalog.RegisterAsync(
            database,
            path,
            SafeDisplayName(allocation.DisplayName),
            size,
            checksum,
            allocation.CreatedAtUtc,
            cancellationToken,
            principalSid).ConfigureAwait(false);

        return ToApproved(entry);
    }

    public async Task<RmsApprovedDatabaseBackup?> ResolveAsync(
        RmsDatabaseKind database,
        string artifactId,
        CancellationToken cancellationToken = default,
        string? principalSid = null)
    {
        if (string.IsNullOrWhiteSpace(artifactId)
            || artifactId.Length > 128
            || artifactId.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            return null;
        }

        var entry = await catalog.ResolveAsync(database, artifactId, cancellationToken, principalSid).ConfigureAwait(false);
        if (entry is null)
        {
            return null;
        }

        var artifact = await InspectAsync(entry, cancellationToken).ConfigureAwait(false);
        return artifact.Availability == RmsDatabaseBackupAvailability.Available
            ? artifact
            : null;
    }

    public async Task<IReadOnlyList<RmsApprovedDatabaseBackup>> ListAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken = default,
        string? principalSid = null)
    {
        var entries = await catalog.ListAsync(database, cancellationToken, principalSid).ConfigureAwait(false);
        var result = new List<RmsApprovedDatabaseBackup>(entries.Count);
        foreach (var entry in entries)
        {
            var artifact = await InspectAsync(entry, cancellationToken).ConfigureAwait(false);
            if (artifact.Availability == RmsDatabaseBackupAvailability.Available)
            {
                result.Add(artifact);
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<RmsApprovedDatabaseBackup>> ListInventoryAsync(
        RmsDatabaseKind database,
        string principalSid,
        CancellationToken cancellationToken = default)
    {
        var entries = await catalog.ListInventoryAsync(database, principalSid, cancellationToken).ConfigureAwait(false);
        var result = new List<RmsApprovedDatabaseBackup>(entries.Count);
        foreach (var entry in entries)
        {
            result.Add(await InspectAsync(entry, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    public Task<bool> RevokeAsync(
        RmsDatabaseKind database,
        string artifactId,
        string principalSid,
        CancellationToken cancellationToken = default) =>
        catalog.RevokeAsync(database, artifactId, principalSid, cancellationToken);

    private async Task<RmsApprovedDatabaseBackup> InspectAsync(
        RmsDatabaseBackupCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(Path.Combine(options.BackupRootPath, entry.FileName));
        var expiresAtUtc = entry.CreatedAtUtc + options.BackupRetention;
        var availability = timeProvider.GetUtcNow() >= expiresAtUtc
            ? RmsDatabaseBackupAvailability.Expired
            : await IsPhysicallyValidAsync(entry, path, cancellationToken).ConfigureAwait(false);
        return ToApproved(entry, path, expiresAtUtc, availability);
    }

    private RmsApprovedDatabaseBackup ToApproved(
        RmsDatabaseBackupCatalogEntry entry,
        string? path = null,
        DateTimeOffset? expiresAtUtc = null,
        RmsDatabaseBackupAvailability availability = RmsDatabaseBackupAvailability.Available) =>
        new(
            entry.Database,
            entry.ArtifactId,
            entry.DisplayName,
            entry.SizeBytes,
            entry.Sha256Checksum,
            entry.CreatedAtUtc,
            expiresAtUtc ?? entry.CreatedAtUtc + options.BackupRetention,
            path ?? Path.GetFullPath(Path.Combine(options.BackupRootPath, entry.FileName)),
            availability,
            entry.PrincipalSid);

    private async Task<RmsDatabaseBackupAvailability> IsPhysicallyValidAsync(
        RmsDatabaseBackupCatalogEntry entry,
        string path,
        CancellationToken cancellationToken)
    {
        if (!BackupPathSafety.IsSafePath(fileSystem, options.BackupRootPath, path)
            || !string.Equals(Path.GetExtension(path), ".bak", StringComparison.OrdinalIgnoreCase)
            || !fileSystem.FileExists(path)
            || fileSystem.IsReparsePoint(path))
        {
            return RmsDatabaseBackupAvailability.Missing;
        }

        try
        {
            if (fileSystem.GetFileLength(path) != entry.SizeBytes)
            {
                return RmsDatabaseBackupAvailability.ChecksumMismatch;
            }

            var checksum = await fileSystem.ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
            return string.Equals(checksum, entry.Sha256Checksum, StringComparison.OrdinalIgnoreCase)
                ? RmsDatabaseBackupAvailability.Available
                : RmsDatabaseBackupAvailability.ChecksumMismatch;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return RmsDatabaseBackupAvailability.Invalid;
        }
    }

    private void EnsureSafeExistingDirectory(string path)
    {
        if (!Directory.Exists(path)
            || !BackupPathSafety.IsSafePath(fileSystem, options.BackupRootPath, Path.Combine(path, "placeholder.bak")))
        {
            throw new InvalidOperationException("The Agent-owned backup destination is unavailable.");
        }
    }

    private static string SafeDisplayName(string value)
    {
        var name = Path.GetFileName(value?.Replace('\\', '/') ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Any(char.IsControl))
        {
            return "backup.bak";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "backup.bak" : safe[..Math.Min(128, safe.Length)];
    }
}
