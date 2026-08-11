using RmsSupportHub.Pos.Contracts.V1.Artifacts;
using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Agent.Artifacts;

/// <summary>
/// In-memory capability catalog for artifacts produced by this Agent instance. Metadata is bounded
/// by <see cref="RuntimeRetentionPolicy.MaxArtifacts"/> and expires at the explicit timestamp in
/// the DTO. Valid artifacts are never evicted merely to make room: registration fails closed when
/// every catalog slot is still within its retention window. Active download leases protect the
/// backing file from expiry cleanup until the response stream is disposed.
/// </summary>
public sealed class ArtifactCatalog
{
    private readonly object _gate = new();
    private readonly IBackupFileSystem _fileSystem;
    private readonly TimeProvider _timeProvider;
    private readonly RuntimeRetentionPolicy _retention;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public ArtifactCatalog(IBackupFileSystem fileSystem)
        : this(fileSystem, TimeProvider.System, RuntimeRetentionPolicy.Default)
    {
    }

    public ArtifactCatalog(IBackupFileSystem fileSystem, TimeProvider timeProvider)
        : this(fileSystem, timeProvider, RuntimeRetentionPolicy.Default)
    {
    }

    public ArtifactCatalog(IBackupFileSystem fileSystem, TimeProvider timeProvider, RuntimeRetentionPolicy retention)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _retention = retention ?? throw new ArgumentNullException(nameof(retention));
        _retention.Validate();
    }

    public ArtifactMetadataDto Register(
        string principal,
        string displayName,
        string path,
        long sizeBytes,
        string sha256Checksum,
        DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(principal))
        {
            throw new ArgumentException("An artifact principal is required.", nameof(principal));
        }

        Prune();
        var metadata = new ArtifactMetadataDto(
            Guid.NewGuid().ToString("N"),
            SafeDisplayName(displayName),
            Math.Max(0, sizeBytes),
            SafeChecksum(sha256Checksum),
            createdAtUtc,
            createdAtUtc.Add(_retention.ArtifactLifetime));
        var entry = new Entry(principal, path, metadata);
        var capacityReached = false;
        var pathAlreadyCataloged = false;

        lock (_gate)
        {
            // Valid artifacts retain their promise to the caller. A full catalog rejects the new
            // artifact and removes its unregistered file instead of deleting a valid old one.
            if (_entries.Count >= _retention.MaxArtifacts)
            {
                capacityReached = true;
                pathAlreadyCataloged = _entries.Values.Any(existing =>
                    string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                _entries[metadata.ArtifactId] = entry;
            }
        }

        if (capacityReached)
        {
            if (!pathAlreadyCataloged)
            {
                DeleteUnregisteredFile(path);
            }

            throw new ArtifactCatalogCapacityException();
        }

        return metadata;
    }

    public IReadOnlyList<ArtifactMetadataDto> List(string principal)
    {
        Prune();
        var retired = new List<Entry>();
        List<ArtifactMetadataDto> result;
        lock (_gate)
        {
            result = [];
            foreach (var pair in _entries.ToArray())
            {
                var entry = pair.Value;
                if (!string.Equals(entry.Principal, principal, StringComparison.Ordinal))
                {
                    continue;
                }

                if (entry.ActiveDownloads > 0 || FileExistsSafely(entry.Path))
                {
                    result.Add(entry.Metadata);
                }
                else if (TryRetireLocked(pair.Key, entry, out var removed))
                {
                    retired.Add(removed!);
                }
            }
        }

        DeleteRetiredFiles(retired);
        return result.OrderByDescending(entry => entry.CreatedAtUtc).ToList();
    }

    public bool TryGet(string principal, string artifactId, out ArtifactMetadataDto? metadata)
    {
        Prune();
        Entry? entry;
        Entry? retired = null;
        lock (_gate)
        {
            if (!_entries.TryGetValue(artifactId, out entry)
                || !string.Equals(entry.Principal, principal, StringComparison.Ordinal))
            {
                metadata = null;
                return false;
            }

            // A response stream that is already in flight remains valid even if the file has been
            // removed externally; a new metadata/download lookup fails closed once no lease exists.
            if (entry.ActiveDownloads == 0 && !FileExistsSafely(entry.Path)
                && TryRetireLocked(artifactId, entry, out retired))
            {
                metadata = null;
            }
            else
            {
                metadata = entry.Metadata;
            }
        }

        if (retired is not null)
        {
            DeleteRetiredFiles([retired]);
        }

        return metadata is not null;
    }

    public async Task<Stream?> OpenReadAsync(string principal, string artifactId, CancellationToken cancellationToken)
    {
        Prune();
        Entry? entry = null;
        Entry? retired = null;
        lock (_gate)
        {
            var principalMatches = _entries.TryGetValue(artifactId, out entry)
                && string.Equals(entry.Principal, principal, StringComparison.Ordinal);
            if (!principalMatches
                || entry is null
                || entry.Metadata.ExpiresAtUtc is not { } expiresAtUtc
                || _timeProvider.GetUtcNow() >= expiresAtUtc
                || !FileExistsSafely(entry.Path))
            {
                if (principalMatches
                    && entry is not null
                    && entry.ActiveDownloads == 0
                    && TryRetireLocked(artifactId, entry, out retired))
                {
                    // Cleanup is performed outside the catalog lock.
                }

                entry = null;
            }
            else
            {
                entry.ActiveDownloads++;
            }
        }

        if (retired is not null)
        {
            DeleteRetiredFiles([retired]);
        }

        if (entry is null)
        {
            return null;
        }

        try
        {
            var stream = await _fileSystem.OpenReadAsync(entry.Path, cancellationToken).ConfigureAwait(false);
            return new ArtifactDownloadStream(stream, () => ReleaseDownload(entry));
        }
        catch
        {
            ReleaseDownload(entry);
            return null;
        }
    }

    /// <summary>Removes only expired entries that have no active download lease.</summary>
    public int Prune()
    {
        var retired = new List<Entry>();
        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            foreach (var pair in _entries.ToArray())
            {
                var entry = pair.Value;
                if (entry.ActiveDownloads == 0
                    && entry.Metadata.ExpiresAtUtc is { } expiresAtUtc
                    && now >= expiresAtUtc
                    && TryRetireLocked(pair.Key, entry, out var removed))
                {
                    retired.Add(removed!);
                }
            }
        }

        DeleteRetiredFiles(retired);
        return retired.Count;
    }

    private void ReleaseDownload(Entry entry)
    {
        Entry? retired = null;
        lock (_gate)
        {
            if (entry.ActiveDownloads > 0)
            {
                entry.ActiveDownloads--;
            }

            if (entry.ActiveDownloads == 0
                && entry.Metadata.ExpiresAtUtc is { } expiresAtUtc
                && _timeProvider.GetUtcNow() >= expiresAtUtc
                && TryRetireLocked(entry.Metadata.ArtifactId, entry, out retired))
            {
                // Cleanup happens outside the catalog lock.
            }
        }

        if (retired is not null)
        {
            DeleteRetiredFiles([retired]);
        }
    }

    private bool TryRetireLocked(string artifactId, Entry entry, out Entry? retired)
    {
        if (entry.ActiveDownloads > 0
            || !_entries.TryGetValue(artifactId, out var current)
            || !ReferenceEquals(current, entry))
        {
            retired = null;
            return false;
        }

        _entries.Remove(artifactId);
        entry.Retired = true;
        retired = entry;
        return true;
    }

    private void DeleteRetiredFiles(IEnumerable<Entry> entries)
    {
        foreach (var entry in entries)
        {
            try
            {
                _fileSystem.DeleteFileAsync(entry.Path, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch
            {
                // The capability is already retired, so a failed physical cleanup cannot create a
                // browser-readable artifact or expose the storage path.
            }
        }
    }

    private void DeleteUnregisteredFile(string path)
    {
        try
        {
            _fileSystem.DeleteFileAsync(path, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
            // Do not expose a storage failure or path through the browser contract.
        }
    }

    private bool FileExistsSafely(string path)
    {
        try
        {
            return _fileSystem.FileExists(path);
        }
        catch
        {
            return false;
        }
    }

    private static string SafeDisplayName(string value)
    {
        var name = Path.GetFileName(value?.Replace('\\', '/') ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Any(char.IsControl))
        {
            return "backup-artifact.zip";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "backup-artifact.zip" : safe[..Math.Min(128, safe.Length)];
    }

    private static string SafeChecksum(string value)
    {
        var checksum = value ?? string.Empty;
        return checksum.Length == 64 && checksum.All(Uri.IsHexDigit)
            ? checksum.ToLowerInvariant()
            : "unavailable";
    }

    private sealed class Entry(string principal, string path, ArtifactMetadataDto metadata)
    {
        public string Principal { get; } = principal;

        public string Path { get; } = path;

        public ArtifactMetadataDto Metadata { get; } = metadata;

        public int ActiveDownloads { get; set; }

        public bool Retired { get; set; }
    }

    private sealed class ArtifactDownloadStream(Stream inner, Action release) : Stream
    {
        private int _released;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.WriteAsync(buffer, offset, count, cancellationToken);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => inner.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    inner.Dispose();
                }
                finally
                {
                    ReleaseOnce();
                }
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            try
            {
                await inner.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                ReleaseOnce();
                GC.SuppressFinalize(this);
            }
        }

        private void ReleaseOnce()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                release();
            }
        }
    }
}

public sealed class ArtifactCatalogCapacityException()
    : InvalidOperationException("The artifact catalog retention limit has been reached.");
