using RmsSupportHub.Pos.Contracts.V1.Artifacts;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Artifacts;

/// <summary>
/// Device-local catalog for RMS database backup artifacts. The catalog accepts only files allocated
/// beneath the fixed Agent-owned backup root and keeps the server path as an internal capability.
/// The browser receives only an opaque artifact ID and sanitized metadata.
/// </summary>
public sealed class RmsDatabaseBackupStorage : IRmsDatabaseBackupStorage
{
    private const string ArtifactPrincipal = "rms-database-backups";
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly IBackupFileSystem fileSystem;
    private readonly ArtifactCatalog artifactCatalog;
    private readonly RmsDatabaseStorageOptions options;

    public RmsDatabaseBackupStorage(
        IBackupFileSystem fileSystem,
        ArtifactCatalog artifactCatalog,
        RmsDatabaseStorageOptions options) : this(fileSystem, artifactCatalog, options, validate: true)
    {
    }

    private RmsDatabaseBackupStorage(
        IBackupFileSystem fileSystem,
        ArtifactCatalog artifactCatalog,
        RmsDatabaseStorageOptions options,
        bool validate)
    {
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.artifactCatalog = artifactCatalog ?? throw new ArgumentNullException(nameof(artifactCatalog));
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
        if (!IsWithinRoot(options.BackupRootPath, path) || !string.Equals(Path.GetExtension(path), ".bak", StringComparison.OrdinalIgnoreCase))
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
        var definition = RmsDatabaseCatalog.For(database);
        var path = Path.GetFullPath(allocation.ServerPath);
        if (!IsSafePath(path)
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

        ArtifactMetadataDto metadata;
        try
        {
            metadata = artifactCatalog.Register(
                ArtifactPrincipal,
                allocation.DisplayName,
                path,
                size,
                checksum,
                allocation.CreatedAtUtc);
        }
        catch (ArtifactCatalogCapacityException)
        {
            return null;
        }

        var approved = new RmsApprovedDatabaseBackup(
            database,
            metadata.ArtifactId,
            metadata.DisplayName,
            metadata.SizeBytes,
            metadata.Sha256Checksum,
            metadata.CreatedAtUtc,
            metadata.ExpiresAtUtc,
            path);

        lock (_gate)
        {
            _entries[metadata.ArtifactId] = new(definition.Kind, approved);
        }

        return approved;
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

        Entry? entry;
        lock (_gate)
        {
            _entries.TryGetValue(artifactId, out entry);
        }

        if (entry is null || entry.Database != database || !artifactCatalog.TryGet(ArtifactPrincipal, artifactId, out var metadata) || metadata is null)
        {
            return null;
        }

        var approved = entry.Backup;
        if (!IsSafePath(approved.ServerPath)
            || !string.Equals(Path.GetExtension(approved.ServerPath), ".bak", StringComparison.OrdinalIgnoreCase)
            || !fileSystem.FileExists(approved.ServerPath)
            || fileSystem.IsReparsePoint(approved.ServerPath))
        {
            return null;
        }

        try
        {
            if (fileSystem.GetFileLength(approved.ServerPath) != metadata.SizeBytes)
            {
                return null;
            }

            var checksum = await fileSystem.ComputeSha256Async(approved.ServerPath, cancellationToken).ConfigureAwait(false);
            return string.Equals(checksum, metadata.Sha256Checksum, StringComparison.OrdinalIgnoreCase)
                ? approved
                : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<RmsApprovedDatabaseBackup> List(RmsDatabaseKind database)
    {
        var metadata = artifactCatalog.List(ArtifactPrincipal)
            .ToDictionary(item => item.ArtifactId, StringComparer.Ordinal);
        lock (_gate)
        {
            foreach (var artifactId in _entries.Keys.Where(id => !metadata.ContainsKey(id)).ToArray())
            {
                _entries.Remove(artifactId);
            }

            return _entries.Values
                .Where(entry => entry.Database == database && metadata.ContainsKey(entry.Backup.ArtifactId))
                .Select(entry => entry.Backup)
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .Take(options.MaximumBackupsPerDatabase)
                .ToArray();
        }
    }

    private bool IsSafePath(string path)
    {
        if (!IsWithinRoot(options.BackupRootPath, path))
        {
            return false;
        }

        var current = path;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if ((fileSystem.FileExists(current) || Directory.Exists(current)) && fileSystem.IsReparsePoint(current))
            {
                return false;
            }

            var parent = Directory.GetParent(current)?.FullName;
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent ?? string.Empty;
        }

        return true;
    }

    private void EnsureSafeExistingDirectory(string path)
    {
        if (!Directory.Exists(path) || !IsSafePath(Path.Combine(path, "placeholder.bak")))
        {
            throw new InvalidOperationException("The Agent-owned backup destination is unavailable.");
        }
    }

    private static bool IsWithinRoot(string root, string target)
    {
        var canonicalRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var canonicalTarget = Path.GetFullPath(target);
        return canonicalTarget.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record Entry(RmsDatabaseKind Database, RmsApprovedDatabaseBackup Backup);
}
