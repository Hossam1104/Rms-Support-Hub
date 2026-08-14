using System.Security.Cryptography;
using System.Text;
using RmsSupportHub.Pos.Agent.Artifacts;
using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Backups;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

/// <summary>
/// Covers the durable, Agent-owned RMS database backup catalog: survival across simulated Agent
/// restarts, fail-closed handling of missing/corrupt/tampered metadata, physical revalidation, and
/// a dedicated retention policy that is independent of the generic <see cref="ArtifactCatalog"/>
/// browser-download lifetime.
/// </summary>
public sealed class RmsDatabaseBackupCatalogTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
    private readonly string _root = Directory.CreateTempSubdirectory("rms-agent-database-backup-catalog-").FullName;

    [Fact]
    public async Task RestartSimulation_BackupRegisteredByInstanceA_IsDiscoverableAndResolvableByInstanceB()
    {
        var options = CreateOptions();
        var catalogA = CreateCatalog(options, new ManualTimeProvider(Start));
        var (path, size, checksum) = await CreateBackupFileAsync("RmsBranchSrv_20260811_120000_a.bak", "branch-backup-content");

        var registered = await catalogA.RegisterAsync(
            RmsDatabaseKind.Branch, path, "RmsBranchSrv_20260811_120000_a.bak", size, checksum, Start, CancellationToken.None);

        // A fresh instance simulates the Agent process restarting; it must load the same durable
        // metadata from disk rather than starting with an empty in-memory catalog.
        var catalogB = CreateCatalog(options, new ManualTimeProvider(Start.AddMinutes(5)));

        var resolved = await catalogB.ResolveAsync(RmsDatabaseKind.Branch, registered.ArtifactId, CancellationToken.None);
        Assert.NotNull(resolved);
        Assert.Equal(registered.ArtifactId, resolved!.ArtifactId);
        Assert.Equal(checksum, resolved.Sha256Checksum);

        var listed = await catalogB.ListAsync(RmsDatabaseKind.Branch, CancellationToken.None);
        Assert.Contains(listed, entry => entry.ArtifactId == registered.ArtifactId);
    }

    [Fact]
    public async Task MissingCatalogFile_ReturnsEmptyCatalogRatherThanThrowing()
    {
        var catalog = CreateCatalog(CreateOptions(), new ManualTimeProvider(Start));

        var listed = await catalog.ListAsync(RmsDatabaseKind.Branch, CancellationToken.None);
        var resolved = await catalog.ResolveAsync(RmsDatabaseKind.Branch, "any-artifact-id", CancellationToken.None);

        Assert.Empty(listed);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task CorruptCatalogFile_FailsClosedToEmptyCatalog()
    {
        var options = CreateOptions();
        Directory.CreateDirectory(Path.Combine(options.BackupRootPath, ".catalog"));
        await File.WriteAllTextAsync(Path.Combine(options.BackupRootPath, ".catalog", "backups.v1.json"), "{ not-valid-json ");

        var catalog = CreateCatalog(options, new ManualTimeProvider(Start));

        var listed = await catalog.ListAsync(RmsDatabaseKind.Branch, CancellationToken.None);
        Assert.Empty(listed);
    }

    [Fact]
    public async Task MissingPhysicalBackupFile_IsExcludedFromResolveAndList()
    {
        var options = CreateOptions();
        var catalog = CreateCatalog(options, new ManualTimeProvider(Start));
        var (path, size, checksum) = await CreateBackupFileAsync("RmsCashierSrv_missing.bak", "cashier-backup-content");
        var registered = await catalog.RegisterAsync(
            RmsDatabaseKind.Cashier, path, "RmsCashierSrv_missing.bak", size, checksum, Start, CancellationToken.None);

        File.Delete(path);

        Assert.Null(await catalog.ResolveAsync(RmsDatabaseKind.Cashier, registered.ArtifactId, CancellationToken.None));
        Assert.Empty(await catalog.ListAsync(RmsDatabaseKind.Cashier, CancellationToken.None));
    }

    [Fact]
    public async Task ChecksumMismatch_IsRejectedWithoutBecomingAnApprovedRestoreSource()
    {
        var options = CreateOptions();
        var catalog = CreateCatalog(options, new ManualTimeProvider(Start));
        var (path, size, checksum) = await CreateBackupFileAsync("RmsBranchSrv_tampered.bak", "original-backup-bytes");
        var registered = await catalog.RegisterAsync(
            RmsDatabaseKind.Branch, path, "RmsBranchSrv_tampered.bak", size, checksum, Start, CancellationToken.None);

        // Same length, different bytes, so the size check alone would not catch the tampering.
        await File.WriteAllTextAsync(path, "replaced-backup-byte5");

        Assert.Null(await catalog.ResolveAsync(RmsDatabaseKind.Branch, registered.ArtifactId, CancellationToken.None));
    }

    [Fact]
    public async Task PathTraversalMetadata_IsRejectedOnLoad()
    {
        var options = CreateOptions();
        var catalogDirectory = Path.Combine(options.BackupRootPath, ".catalog");
        Directory.CreateDirectory(catalogDirectory);
        var maliciousJson = """
            {"SchemaVersion":1,"Entries":[{"ArtifactId":"evil","Database":"Branch","DisplayName":"evil.bak","FileName":"..\\..\\escape.bak","SizeBytes":10,"Sha256Checksum":"0000000000000000000000000000000000000000000000000000000000000000","CreatedAtUtc":"2026-08-11T12:00:00+00:00"}]}
            """;
        await File.WriteAllTextAsync(Path.Combine(catalogDirectory, "backups.v1.json"), maliciousJson);

        var catalog = CreateCatalog(options, new ManualTimeProvider(Start));

        Assert.Null(await catalog.ResolveAsync(RmsDatabaseKind.Branch, "evil", CancellationToken.None));
        Assert.Empty(await catalog.ListAsync(RmsDatabaseKind.Branch, CancellationToken.None));
    }

    [Fact]
    public async Task ReparsePointBackupFile_IsRejected()
    {
        var options = CreateOptions();
        var fileSystem = new ReparseSimulatingFileSystem();
        var catalog = new RmsDatabaseBackupCatalog(fileSystem, options, new ManualTimeProvider(Start));
        var (path, size, checksum) = await CreateBackupFileAsync("RmsBranchSrv_reparse.bak", "reparse-backup-content");
        var registered = await catalog.RegisterAsync(
            RmsDatabaseKind.Branch, path, "RmsBranchSrv_reparse.bak", size, checksum, Start, CancellationToken.None);

        fileSystem.ReparsePaths.Add(Path.GetFullPath(path));

        Assert.Null(await catalog.ResolveAsync(RmsDatabaseKind.Branch, registered.ArtifactId, CancellationToken.None));
    }

    [Fact]
    public async Task WrongTargetAssociation_IsRejected()
    {
        var options = CreateOptions();
        var catalog = CreateCatalog(options, new ManualTimeProvider(Start));
        var (path, size, checksum) = await CreateBackupFileAsync("RmsBranchSrv_target.bak", "branch-only-content");
        var registered = await catalog.RegisterAsync(
            RmsDatabaseKind.Branch, path, "RmsBranchSrv_target.bak", size, checksum, Start, CancellationToken.None);

        Assert.Null(await catalog.ResolveAsync(RmsDatabaseKind.Cashier, registered.ArtifactId, CancellationToken.None));
    }

    [Fact]
    public async Task RetentionByCount_PrunesOldestBeyondMaximumAndDeletesItsPhysicalFile()
    {
        var options = CreateOptions(maximumBackupsPerDatabase: 2);
        var clock = new ManualTimeProvider(Start);
        var catalog = CreateCatalog(options, clock);

        var first = await RegisterAtAsync(catalog, "first.bak", "first-content", clock.GetUtcNow());
        clock.Advance(TimeSpan.FromMinutes(1));
        var second = await RegisterAtAsync(catalog, "second.bak", "second-content", clock.GetUtcNow());
        clock.Advance(TimeSpan.FromMinutes(1));
        var third = await RegisterAtAsync(catalog, "third.bak", "third-content", clock.GetUtcNow());

        var listed = await catalog.ListAsync(RmsDatabaseKind.Branch, CancellationToken.None);
        Assert.Equal(2, listed.Count);
        Assert.DoesNotContain(listed, entry => entry.ArtifactId == first.Entry.ArtifactId);
        Assert.Contains(listed, entry => entry.ArtifactId == second.Entry.ArtifactId);
        Assert.Contains(listed, entry => entry.ArtifactId == third.Entry.ArtifactId);
        Assert.False(File.Exists(first.Path), "The pruned oldest backup's physical file should be deleted.");
        Assert.True(File.Exists(second.Path));
        Assert.True(File.Exists(third.Path));
    }

    [Fact]
    public async Task RetentionByAge_PrunesEntriesOlderThanBackupRetention()
    {
        var options = CreateOptions(backupRetention: TimeSpan.FromDays(1));
        var clock = new ManualTimeProvider(Start);
        var catalog = CreateCatalog(options, clock);

        var old = await RegisterAtAsync(catalog, "old.bak", "old-content", clock.GetUtcNow());
        clock.Advance(TimeSpan.FromDays(2));
        var recent = await RegisterAtAsync(catalog, "recent.bak", "recent-content", clock.GetUtcNow());

        var listed = await catalog.ListAsync(RmsDatabaseKind.Branch, CancellationToken.None);
        Assert.DoesNotContain(listed, entry => entry.ArtifactId == old.Entry.ArtifactId);
        Assert.Contains(listed, entry => entry.ArtifactId == recent.Entry.ArtifactId);
        Assert.False(File.Exists(old.Path));
    }

    [Fact]
    public async Task DatabaseBackupSurvivesPastTheGenericArtifactCatalogDownloadLifetime()
    {
        // The generic browser-download lifetime (RuntimeRetentionPolicy.ArtifactLifetime) defaults to
        // 24 hours; a physical database backup must not be pruned merely because that unrelated
        // window has elapsed. The dedicated BackupRetention default (30 days) governs it instead.
        var options = CreateOptions();
        var clock = new ManualTimeProvider(Start);
        var catalog = CreateCatalog(options, clock);
        var registered = await RegisterAtAsync(catalog, "long-lived.bak", "long-lived-content", clock.GetUtcNow());

        clock.Advance(TimeSpan.FromHours(48));

        var listed = await catalog.ListAsync(RmsDatabaseKind.Branch, CancellationToken.None);
        Assert.Contains(listed, entry => entry.ArtifactId == registered.Entry.ArtifactId);
        Assert.True(File.Exists(registered.Path));
        Assert.NotNull(await catalog.ResolveAsync(RmsDatabaseKind.Branch, registered.Entry.ArtifactId, CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private RmsDatabaseStorageOptions CreateOptions(
        int? maximumBackupsPerDatabase = null,
        TimeSpan? backupRetention = null)
    {
        var defaults = new RmsDatabaseStorageOptions();
        return new RmsDatabaseStorageOptions
        {
            BackupRootPath = Path.Combine(_root, "backups"),
            DatabaseFilesRootPath = Path.Combine(_root, "database-files"),
            MaximumBackupsPerDatabase = maximumBackupsPerDatabase ?? defaults.MaximumBackupsPerDatabase,
            BackupRetention = backupRetention ?? defaults.BackupRetention
        };
    }

    private static RmsDatabaseBackupCatalog CreateCatalog(RmsDatabaseStorageOptions options, TimeProvider clock) =>
        new(new PhysicalBackupFileSystem(), options, clock);

    private async Task<(string Path, long Size, string Checksum)> CreateBackupFileAsync(string fileName, string content)
    {
        Directory.CreateDirectory(Path.Combine(_root, "backups"));
        var path = Path.Combine(_root, "backups", fileName);
        var bytes = Encoding.UTF8.GetBytes(content);
        await File.WriteAllBytesAsync(path, bytes);
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return (path, bytes.LongLength, checksum);
    }

    private async Task<(RmsDatabaseBackupCatalogEntry Entry, string Path)> RegisterAtAsync(
        RmsDatabaseBackupCatalog catalog,
        string fileName,
        string content,
        DateTimeOffset createdAtUtc)
    {
        var (path, size, checksum) = await CreateBackupFileAsync(fileName, content);
        var entry = await catalog.RegisterAsync(RmsDatabaseKind.Branch, path, fileName, size, checksum, createdAtUtc, CancellationToken.None);
        return (entry, path);
    }

    /// <summary>Wraps the real filesystem but lets a test force specific paths to report as reparse points.</summary>
    private sealed class ReparseSimulatingFileSystem : IBackupFileSystem
    {
        private readonly PhysicalBackupFileSystem inner = new();

        public HashSet<string> ReparsePaths { get; } = new(StringComparer.OrdinalIgnoreCase);

        public BackupDestinationInfo InspectDestination(string path) => inner.InspectDestination(path);

        public bool FileExists(string path) => inner.FileExists(path);

        public bool IsReparsePoint(string path) => ReparsePaths.Contains(Path.GetFullPath(path)) || inner.IsReparsePoint(path);

        public long GetFileLength(string path) => inner.GetFileLength(path);

        public Task EnsureDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
            inner.EnsureDirectoryAsync(path, cancellationToken);

        public Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default) =>
            inner.CopyFileAsync(sourcePath, destinationPath, cancellationToken);

        public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default) =>
            inner.OpenReadAsync(path, cancellationToken);

        public Task<Stream> CreateFileAsync(string path, CancellationToken cancellationToken = default) =>
            inner.CreateFileAsync(path, cancellationToken);

        public Task MoveFileAsync(string sourcePath, string destinationPath, bool overwrite, CancellationToken cancellationToken = default) =>
            inner.MoveFileAsync(sourcePath, destinationPath, overwrite, cancellationToken);

        public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default) =>
            inner.DeleteFileAsync(path, cancellationToken);

        public Task DeleteDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
            inner.DeleteDirectoryAsync(path, cancellationToken);

        public Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default) =>
            inner.ComputeSha256Async(path, cancellationToken);
    }
}
