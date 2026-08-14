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

    public RmsDatabaseBackupStorage(
        IBackupFileSystem fileSystem,
        RmsDatabaseBackupCatalog catalog,
        RmsDatabaseStorageOptions options) : this(fileSystem, catalog, options, validate: true)
    {
    }

    private RmsDatabaseBackupStorage(
        IBackupFileSystem fileSystem,
        RmsDatabaseBackupCatalog catalog,
        RmsDatabaseStorageOptions options,
        bool validate)
    {
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
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
        CancellationToken cancellationToken = default)
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
            if (size <= 0)
            {
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
            cancellationToken).ConfigureAwait(false);

        return ToApproved(entry);
    }

    public async Task<RmsApprovedDatabaseBackup?> ResolveAsync(
        RmsDatabaseKind database,
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactId)
            || artifactId.Length > 128
            || artifactId.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            return null;
        }

        var entry = await catalog.ResolveAsync(database, artifactId, cancellationToken).ConfigureAwait(false);
        return entry is null ? null : ToApproved(entry);
    }

    public async Task<IReadOnlyList<RmsApprovedDatabaseBackup>> ListAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken = default)
    {
        var entries = await catalog.ListAsync(database, cancellationToken).ConfigureAwait(false);
        return entries.Select(ToApproved).ToArray();
    }

    private RmsApprovedDatabaseBackup ToApproved(RmsDatabaseBackupCatalogEntry entry) =>
        new(
            entry.Database,
            entry.ArtifactId,
            entry.DisplayName,
            entry.SizeBytes,
            entry.Sha256Checksum,
            entry.CreatedAtUtc,
            null,
            Path.GetFullPath(Path.Combine(options.BackupRootPath, entry.FileName)));

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
