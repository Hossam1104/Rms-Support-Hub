using System.Text.Json;
using System.Text.Json.Serialization;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Artifacts;

/// <summary>
/// One durable, Agent-owned catalog record for an approved RMS database backup. <see cref="FileName"/>
/// is a bare file name (never a path) resolved beneath the approved backup root at read time; no
/// caller-controlled absolute path is ever persisted.
/// </summary>
public sealed record RmsDatabaseBackupCatalogEntry(
    string ArtifactId,
    RmsDatabaseKind Database,
    string DisplayName,
    string FileName,
    long SizeBytes,
    string Sha256Checksum,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Durable, Agent-owned catalog of RMS database backup artifacts. Metadata is persisted as a single
/// JSON document beneath the approved backup root so approved backups remain discoverable and
/// restorable after an Agent process restart, a Windows reboot, or a Support Hub restart. Only
/// entries recorded here are ever treated as approved restore sources: a <c>.bak</c> file that
/// appears in the backup root without a matching catalog entry is never auto-imported.
///
/// Retention (both a maximum count per database and a maximum age) is owned and enforced entirely
/// here, independently of the generic <see cref="ArtifactCatalog"/> browser-download lifetime, so a
/// database backup is never deleted merely because a browser download capability expired.
/// </summary>
public sealed class RmsDatabaseBackupCatalog
{
    private const int SchemaVersion = 1;
    private const string CatalogDirectoryName = ".catalog";
    private const string CatalogFileName = "backups.v1.json";

    // A legitimate Agent-owned catalog document is small; a file larger than this ceiling cannot be
    // genuine and is treated as corrupt rather than parsed as an unbounded payload.
    private const long MaxCatalogFileBytes = 4 * 1024 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IBackupFileSystem fileSystem;
    private readonly RmsDatabaseStorageOptions options;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);
    private Dictionary<string, RmsDatabaseBackupCatalogEntry>? cache;

    public RmsDatabaseBackupCatalog(
        IBackupFileSystem fileSystem,
        RmsDatabaseStorageOptions options,
        TimeProvider timeProvider)
    {
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    private string CatalogDirectory => Path.Combine(options.BackupRootPath, CatalogDirectoryName);

    private string CatalogPath => Path.Combine(CatalogDirectory, CatalogFileName);

    /// <summary>Adds a catalog entry for a backup file already written beneath the approved root, then applies retention.</summary>
    public async Task<RmsDatabaseBackupCatalogEntry> RegisterAsync(
        RmsDatabaseKind database,
        string absolutePath,
        string displayName,
        long sizeBytes,
        string sha256Checksum,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await LoadLockedAsync(cancellationToken).ConfigureAwait(false);
            var entry = new RmsDatabaseBackupCatalogEntry(
                Guid.NewGuid().ToString("N"),
                database,
                displayName,
                Path.GetFileName(absolutePath),
                sizeBytes,
                sha256Checksum,
                createdAtUtc);

            entries[entry.ArtifactId] = entry;
            var evicted = ApplyRetentionLocked(entries, database);
            await SaveLockedAsync(entries, cancellationToken).ConfigureAwait(false);
            await DeleteEvictedFilesAsync(evicted, cancellationToken).ConfigureAwait(false);
            return entry;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Resolves a catalog-owned entry, revalidating that its physical backup still matches the recorded size and checksum.</summary>
    public async Task<RmsDatabaseBackupCatalogEntry?> ResolveAsync(
        RmsDatabaseKind database,
        string artifactId,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await LoadLockedAsync(cancellationToken).ConfigureAwait(false);
            if (!entries.TryGetValue(artifactId, out var entry) || entry.Database != database)
            {
                return null;
            }

            return await IsPhysicallyValidAsync(entry, cancellationToken).ConfigureAwait(false) ? entry : null;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Lists physically-valid catalog entries for one canonical database, newest first, bounded by the configured maximum.</summary>
    public async Task<IReadOnlyList<RmsDatabaseBackupCatalogEntry>> ListAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await LoadLockedAsync(cancellationToken).ConfigureAwait(false);
            var result = new List<RmsDatabaseBackupCatalogEntry>();
            foreach (var entry in entries.Values
                         .Where(candidate => candidate.Database == database)
                         .OrderByDescending(candidate => candidate.CreatedAtUtc))
            {
                if (await IsPhysicallyValidAsync(entry, cancellationToken).ConfigureAwait(false))
                {
                    result.Add(entry);
                }
            }

            return result.Take(options.MaximumBackupsPerDatabase).ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    private string ResolveAbsolutePath(RmsDatabaseBackupCatalogEntry entry) =>
        Path.GetFullPath(Path.Combine(options.BackupRootPath, entry.FileName));

    private async Task<bool> IsPhysicallyValidAsync(RmsDatabaseBackupCatalogEntry entry, CancellationToken cancellationToken)
    {
        var path = ResolveAbsolutePath(entry);
        if (!BackupPathSafety.IsSafePath(fileSystem, options.BackupRootPath, path)
            || !string.Equals(Path.GetExtension(path), ".bak", StringComparison.OrdinalIgnoreCase)
            || !fileSystem.FileExists(path)
            || fileSystem.IsReparsePoint(path))
        {
            return false;
        }

        try
        {
            if (fileSystem.GetFileLength(path) != entry.SizeBytes)
            {
                return false;
            }

            var checksum = await fileSystem.ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
            return string.Equals(checksum, entry.Sha256Checksum, StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Removes entries beyond the count cap or older than the age cutoff for one database; caller deletes the returned files.</summary>
    private List<RmsDatabaseBackupCatalogEntry> ApplyRetentionLocked(
        Dictionary<string, RmsDatabaseBackupCatalogEntry> entries,
        RmsDatabaseKind database)
    {
        var cutoff = timeProvider.GetUtcNow() - options.BackupRetention;
        var scoped = entries.Values
            .Where(entry => entry.Database == database)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ToList();

        var evicted = new List<RmsDatabaseBackupCatalogEntry>();
        for (var index = 0; index < scoped.Count; index++)
        {
            var entry = scoped[index];
            if (index >= options.MaximumBackupsPerDatabase || entry.CreatedAtUtc < cutoff)
            {
                entries.Remove(entry.ArtifactId);
                evicted.Add(entry);
            }
        }

        return evicted;
    }

    private async Task DeleteEvictedFilesAsync(
        IEnumerable<RmsDatabaseBackupCatalogEntry> evicted,
        CancellationToken cancellationToken)
    {
        foreach (var entry in evicted)
        {
            var path = ResolveAbsolutePath(entry);

            // Only ever delete a file this catalog owns beneath the approved root, and never follow
            // a reparse point while doing so.
            if (!BackupPathSafety.IsSafePath(fileSystem, options.BackupRootPath, path))
            {
                continue;
            }

            try
            {
                await fileSystem.DeleteFileAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // The catalog metadata has already dropped the entry, so a failed physical cleanup
                // cannot make an unapproved file reachable again; it is simply retried on next prune.
            }
        }
    }

    private async Task<Dictionary<string, RmsDatabaseBackupCatalogEntry>> LoadLockedAsync(CancellationToken cancellationToken)
    {
        if (cache is not null)
        {
            return cache;
        }

        var loaded = await ReadCatalogFileAsync(cancellationToken).ConfigureAwait(false);
        cache = loaded;
        return loaded;
    }

    private async Task<Dictionary<string, RmsDatabaseBackupCatalogEntry>> ReadCatalogFileAsync(CancellationToken cancellationToken)
    {
        if (!fileSystem.FileExists(CatalogPath) || fileSystem.IsReparsePoint(CatalogPath))
        {
            return new(StringComparer.Ordinal);
        }

        try
        {
            if (fileSystem.GetFileLength(CatalogPath) > MaxCatalogFileBytes)
            {
                return new(StringComparer.Ordinal);
            }

            await using var stream = await fileSystem.OpenReadAsync(CatalogPath, cancellationToken).ConfigureAwait(false);
            var document = await JsonSerializer.DeserializeAsync<CatalogDocument>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            if (document is null || document.SchemaVersion != SchemaVersion || document.Entries is null)
            {
                return new(StringComparer.Ordinal);
            }

            var result = new Dictionary<string, RmsDatabaseBackupCatalogEntry>(StringComparer.Ordinal);
            foreach (var entry in document.Entries)
            {
                if (IsWellFormed(entry))
                {
                    result[entry.ArtifactId] = entry;
                }
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Corrupt or unreadable catalog metadata fails closed to an empty catalog rather than
            // surfacing a parser exception; no unapproved backup ever becomes reachable this way.
            return new(StringComparer.Ordinal);
        }
    }

    private static bool IsWellFormed(RmsDatabaseBackupCatalogEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.ArtifactId)
        && !string.IsNullOrWhiteSpace(entry.FileName)
        && !entry.FileName.Contains('/', StringComparison.Ordinal)
        && !entry.FileName.Contains('\\', StringComparison.Ordinal)
        && string.Equals(Path.GetFileName(entry.FileName), entry.FileName, StringComparison.Ordinal)
        && string.Equals(Path.GetExtension(entry.FileName), ".bak", StringComparison.OrdinalIgnoreCase)
        && entry.SizeBytes > 0
        && entry.Sha256Checksum.Length == 64
        && entry.Sha256Checksum.All(Uri.IsHexDigit);

    private async Task SaveLockedAsync(
        Dictionary<string, RmsDatabaseBackupCatalogEntry> entries,
        CancellationToken cancellationToken)
    {
        await fileSystem.EnsureDirectoryAsync(CatalogDirectory, cancellationToken).ConfigureAwait(false);
        var document = new CatalogDocument(SchemaVersion, entries.Values.OrderBy(entry => entry.CreatedAtUtc).ToList());
        var tempPath = Path.Combine(CatalogDirectory, $"{CatalogFileName}.{Guid.NewGuid():N}.tmp");
        await using (var stream = await fileSystem.CreateFileAsync(tempPath, cancellationToken).ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        await fileSystem.MoveFileAsync(tempPath, CatalogPath, overwrite: true, cancellationToken).ConfigureAwait(false);
        cache = entries;
    }

    private sealed record CatalogDocument(int SchemaVersion, List<RmsDatabaseBackupCatalogEntry> Entries);
}
