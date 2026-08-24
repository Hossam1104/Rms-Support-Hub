using RmsSupportHub.Pos.Agent.Artifacts;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class CallerBoundDestinationRootTests : IDisposable
{
    private const string Alice = "S-1-5-21-1111111111-2222222222-3333333333-1001";
    private const string Bob = "S-1-5-21-1111111111-2222222222-3333333333-1002";
    private const string LocalSystem = "S-1-5-18";
    private readonly string root = Directory.CreateTempSubdirectory("rms-caller-roots-").FullName;

    [Fact]
    public void ProductionResolverBindsApprovedRootsToTheAuthenticatedCallerSid()
    {
        var aliceProfile = CreateProfile("Alice");
        var bobProfile = CreateProfile("Bob");
        var systemProfile = CreateProfile("LocalSystem");
        var provider = new FakeProfilePathProvider(new Dictionary<string, string?>
        {
            [Alice] = aliceProfile,
            [Bob] = bobProfile,
            [LocalSystem] = systemProfile
        });
        var resolver = new WindowsProfileListDestinationRootResolver(provider);
        var policy = new LocalArtifactDestinationPolicy(resolver);

        Assert.True(resolver.TryResolve(Alice, out var aliceRoots));
        Assert.All(aliceRoots, item => Assert.Contains("Alice", item.FullPath, StringComparison.OrdinalIgnoreCase));
        Assert.True(policy.TryValidate(Alice, Path.Combine(aliceProfile, "Desktop", "alice.bak"), ".bak", out _));
        Assert.False(policy.TryValidate(Bob, Path.Combine(aliceProfile, "Desktop", "not-bob.bak"), ".bak", out _));
        Assert.False(policy.TryValidate(Alice, Path.Combine(bobProfile, "Desktop", "not-alice.bak"), ".bak", out _));
        Assert.False(policy.TryValidate(Alice, Path.Combine(systemProfile, "Desktop", "local-system.bak"), ".bak", out _));
    }

    [Fact]
    public void UnresolvedCallerProfileAndMissingApprovedRootFailClosed()
    {
        var aliceProfile = CreateProfile("Alice");
        var provider = new FakeProfilePathProvider(new Dictionary<string, string?> { [Alice] = aliceProfile });
        var resolver = new WindowsProfileListDestinationRootResolver(provider);
        var policy = new LocalArtifactDestinationPolicy(resolver);

        Assert.False(resolver.TryResolve(Bob, out _));
        Assert.False(policy.TryValidate(Bob, Path.Combine(aliceProfile, "Desktop", "unknown.bak"), ".bak", out _));

        Directory.Delete(Path.Combine(aliceProfile, "Downloads"));
        Assert.False(resolver.TryResolve(Alice, out _));
    }

    [Fact]
    public void ResolverRejectsNetworkDeviceAndTraversalCandidatesAtTheDestinationPolicy()
    {
        var aliceProfile = CreateProfile("Alice");
        var resolver = new WindowsProfileListDestinationRootResolver(
            new FakeProfilePathProvider(new Dictionary<string, string?> { [Alice] = aliceProfile }));
        var policy = new LocalArtifactDestinationPolicy(resolver);

        Assert.False(policy.TryValidate(Alice, @"\\server\share\backup.bak", ".bak", out _));
        Assert.False(policy.TryValidate(Alice, @"\\.\PhysicalDrive0\backup.bak", ".bak", out _));
        Assert.False(policy.TryValidate(Alice, Path.Combine(aliceProfile, "Desktop", "..", "escape.bak"), ".bak", out _));
        Assert.False(policy.TryValidate(Alice, Path.Combine(aliceProfile, "Desktop", "backup.bak:stream"), ".bak", out _));
        Assert.False(policy.TryValidate(Alice, Path.Combine(aliceProfile, "Desktop", "backup.zip"), ".bak", out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private string CreateProfile(string name)
    {
        var profile = Path.Combine(root, name);
        Directory.CreateDirectory(Path.Combine(profile, "Desktop"));
        Directory.CreateDirectory(Path.Combine(profile, "Documents"));
        Directory.CreateDirectory(Path.Combine(profile, "Downloads"));
        return profile;
    }

    private sealed class FakeProfilePathProvider(IReadOnlyDictionary<string, string?> paths)
        : ILocalCallerProfilePathProvider
    {
        public string? TryGetProfilePath(string principalSid) =>
            paths.TryGetValue(principalSid, out var path) ? path : null;
    }
}
