using RmsSupportHub.Pos.Agent;
using RmsSupportHub.Pos.Agent.Artifacts;
using RmsSupportHub.Pos.Infrastructure.Backups;
using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class ArtifactCatalogTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
    private const string Sid = FakeAuthenticationHandler.DefaultSid;
    private readonly string _root = Directory.CreateTempSubdirectory("rms-agent-artifact-catalog-").FullName;

    [Fact]
    public async Task ArtifactIsDownloadableWithinWindowAndExpiresFailClosed()
    {
        var clock = new ManualTimeProvider(Start);
        var catalog = CreateCatalog(clock);
        var path = CreateArtifact("valid.zip", "artifact-content");
        var metadata = catalog.Register(Sid, "backup.zip", path, new FileInfo(path).Length, "checksum", Start);

        Assert.Equal(Start.AddHours(1), metadata.ExpiresAtUtc);
        Assert.True(catalog.TryGet(Sid, metadata.ArtifactId, out _));
        await using (var stream = await catalog.OpenReadAsync(Sid, metadata.ArtifactId, CancellationToken.None))
        {
            Assert.NotNull(stream);
            Assert.Equal((byte)'a', stream!.ReadByte());
        }

        clock.Advance(TimeSpan.FromHours(1));

        Assert.False(catalog.TryGet(Sid, metadata.ArtifactId, out _));
        Assert.False(File.Exists(path));
        Assert.Null(await catalog.OpenReadAsync(Sid, metadata.ArtifactId, CancellationToken.None));
    }

    [Fact]
    public async Task ExpiryDoesNotDeleteAnActiveDownloadUntilDisposed()
    {
        var clock = new ManualTimeProvider(Start);
        var catalog = CreateCatalog(clock);
        var path = CreateArtifact("leased.zip", "artifact-content");
        var metadata = catalog.Register(Sid, "leased.zip", path, new FileInfo(path).Length, "checksum", Start);
        var stream = await catalog.OpenReadAsync(Sid, metadata.ArtifactId, CancellationToken.None);

        clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(0, catalog.Prune());
        Assert.True(File.Exists(path));
        Assert.True(catalog.TryGet(Sid, metadata.ArtifactId, out _));

        await stream!.DisposeAsync();

        Assert.False(File.Exists(path));
        Assert.False(catalog.TryGet(Sid, metadata.ArtifactId, out _));
    }

    [Fact]
    public async Task MissingArtifactFailsClosedWithoutRevealingStorage()
    {
        var clock = new ManualTimeProvider(Start);
        var catalog = CreateCatalog(clock);
        var path = CreateArtifact("missing.zip", "artifact-content");
        var metadata = catalog.Register(Sid, "missing.zip", path, new FileInfo(path).Length, "checksum", Start);
        File.Delete(path);

        Assert.False(catalog.TryGet(Sid, metadata.ArtifactId, out _));
        Assert.Null(await catalog.OpenReadAsync(Sid, metadata.ArtifactId, CancellationToken.None));
        Assert.DoesNotContain(_root, metadata.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FullCatalogRefusesNewArtifactWithoutDeletingValidDownload()
    {
        var clock = new ManualTimeProvider(Start);
        var catalog = new ArtifactCatalog(new PhysicalBackupFileSystem(), clock, new RuntimeRetentionPolicy { MaxArtifacts = 1 });
        var firstPath = CreateArtifact("first.zip", "first");
        var secondPath = CreateArtifact("second.zip", "second");
        var first = catalog.Register(Sid, "first.zip", firstPath, 5, "checksum", Start);

        Assert.Throws<ArtifactCatalogCapacityException>(() => catalog.Register(Sid, "second.zip", secondPath, 6, "checksum", Start));
        Assert.True(catalog.TryGet(Sid, first.ArtifactId, out _));
        Assert.True(File.Exists(firstPath));
        Assert.False(File.Exists(secondPath));
    }

    [Fact]
    public async Task ArtifactAccessIsPrincipalScoped()
    {
        var clock = new ManualTimeProvider(Start);
        var catalog = CreateCatalog(clock);
        var path = CreateArtifact("scoped.zip", "artifact-content");
        var metadata = catalog.Register(Sid, "scoped.zip", path, new FileInfo(path).Length, "checksum", Start);

        Assert.False(catalog.TryGet("S-1-5-21-1111111111-2222222222-3333333333-1002", metadata.ArtifactId, out _));
        Assert.Null(await catalog.OpenReadAsync("S-1-5-21-1111111111-2222222222-3333333333-1002", metadata.ArtifactId, CancellationToken.None));
        Assert.True(File.Exists(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ArtifactCatalog CreateCatalog(ManualTimeProvider clock) =>
        new(new PhysicalBackupFileSystem(), clock, new RuntimeRetentionPolicy
        {
            MaxArtifacts = 4,
            ArtifactLifetime = TimeSpan.FromHours(1)
        });

    private string CreateArtifact(string name, string contents)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, contents);
        return path;
    }
}
