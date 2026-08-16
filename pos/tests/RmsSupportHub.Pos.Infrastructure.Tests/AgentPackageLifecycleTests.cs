using System.IO.Compression;
using System.Security.Cryptography;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Configuration;
using RmsSupportHub.Pos.Infrastructure.Packages;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

public sealed class AgentPackageLifecycleTests
{
    [Fact]
    public void LifecycleCheckpointTreatsCorruptionAsRecoveryRequired()
    {
        var root = Path.Combine(Path.GetTempPath(), "RmsSupportHub.Pos.Tests", "rms-agent-checkpoint-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = new AgentPackageOptions
            {
                PackageRoot = Path.Combine(root, "packages"),
                InstallationRoot = Path.Combine(root, "install")
            };
            options.EnsureStorageProvisioned();
            File.WriteAllText(options.LifecycleStatePath, "not-json");
            ServiceOwnedDirectoryProvisioner.EnsureControlFileAcl(options.LifecycleStatePath);

            var store = new AgentPackageLifecycleStateStore(options);

            Assert.Null(store.Read());
            Assert.True(store.HasUnreadableState);

            store.Clear();
            File.WriteAllText(options.LifecycleStatePath, "null");
            ServiceOwnedDirectoryProvisioner.EnsureControlFileAcl(options.LifecycleStatePath);
            var nullStateStore = new AgentPackageLifecycleStateStore(options);
            Assert.Null(nullStateStore.Read());
            Assert.True(nullStateStore.HasUnreadableState);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InstallStagesActivatesAndCommitsOnlyAfterHealth()
    {
        using var fixture = PackageFixture.Create();
        var platform = new RecordingInstallationPlatform { HealthResult = true };
        var lifecycle = fixture.CreateLifecycle(platform);

        var result = await lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Install));

        Assert.True(result.State == AgentPackageOperationState.Completed, $"{result.State}: {result.Detail}");
        Assert.True(platform.Prepared);
        Assert.True(platform.Activated);
        Assert.True(platform.HealthChecked);
        Assert.False(platform.RolledBack);
    }

    [Fact]
    public async Task FailedPostActivationHealthRestoresThePreviousPackageWhenPlatformConfirmsRollback()
    {
        using var fixture = PackageFixture.Create();
        var platform = new RecordingInstallationPlatform { HealthResult = false, RollbackResult = true };
        var lifecycle = fixture.CreateLifecycle(platform);

        var result = await lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Upgrade));

        Assert.True(result.State == AgentPackageOperationState.RollbackSucceeded, $"{result.State}: {result.Detail}");
        Assert.True(result.RollbackAttempted);
        Assert.True(result.RollbackSucceeded);
        Assert.True(platform.RolledBack);
    }

    [Fact]
    public async Task UnsafeArchiveFailsBeforeActivationAndDoesNotClaimRollback()
    {
        using var fixture = PackageFixture.Create(unsafeArchive: true);
        var platform = new RecordingInstallationPlatform { HealthResult = true, RollbackResult = true };
        var lifecycle = fixture.CreateLifecycle(platform);

        var result = await lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Install));

        Assert.Equal(AgentPackageOperationState.Failed, result.State);
        Assert.False(result.RollbackAttempted);
        Assert.False(platform.Activated);
        Assert.False(platform.RolledBack);
    }

    private sealed class RecordingInstallationPlatform : IAgentPackageInstallationPlatform
    {
        public bool HealthResult { get; init; }

        public bool RollbackResult { get; init; }

        public bool Prepared { get; private set; }

        public bool Activated { get; private set; }

        public bool HealthChecked { get; private set; }

        public bool RolledBack { get; private set; }

        public Task<bool> PrepareAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default)
        {
            Prepared = true;
            return Task.FromResult(true);
        }

        public Task<bool> ActivateAsync(string stagedRoot, AgentPackageManifest manifest, CancellationToken cancellationToken = default)
        {
            Activated = true;
            return Task.FromResult(true);
        }

        public Task<bool> VerifyHealthAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default)
        {
            HealthChecked = true;
            return Task.FromResult(HealthResult);
        }

        public Task<bool> RollbackAsync(AgentPackageOperationKind failedOperation, CancellationToken cancellationToken = default)
        {
            RolledBack = true;
            return Task.FromResult(RollbackResult);
        }
    }

    private sealed class PackageFixture : IDisposable
    {
        private readonly string root;
        private readonly AgentPackageOptions options;
        private readonly AgentPackageManifest manifest;
        private readonly FileAgentPackageVerifier verifier;

        private PackageFixture(string root, AgentPackageOptions options, AgentPackageManifest manifest, FileAgentPackageVerifier verifier)
        {
            this.root = root;
            this.options = options;
            this.manifest = manifest;
            this.verifier = verifier;
        }

        public static PackageFixture Create(bool unsafeArchive = false)
        {
            var root = Path.Combine(Path.GetTempPath(), "RmsSupportHub.Pos.Tests", "rms-agent-lifecycle-" + Guid.NewGuid().ToString("N"));
            var options = new AgentPackageOptions
            {
                PackageRoot = Path.Combine(root, "packages"),
                InstallationRoot = Path.Combine(root, "install"),
                ReleaseChannel = AgentProductIdentity.ReleaseChannelTesting
            };
            options.EnsureStorageProvisioned();

            var fileBytes = new byte[] { 1, 2, 3, 4 };
            var manifest = new AgentPackageManifest(
                "rms-support-agent",
                "1.0.0",
                "Windows",
                "net10.0-windows",
                AgentProductIdentity.ServiceDisplayName,
                "LocalSystem",
                AgentProductIdentity.PermanentServiceName,
                "SHA256withRSA",
                "DBS test signer",
                new string('0', 64),
                "test-signature",
                [new AgentPackageFileManifest("agent", "RmsSupportAgent.exe", fileBytes.Length, Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant(), true)],
                ["AgentServiceReadWrite"],
                ["AgentHttpsCertificate"],
                null,
                false,
                1,
                1,
                AgentProductIdentity.ProductId,
                "x64",
                AgentProductIdentity.ReleaseChannelTesting,
                AgentProductIdentity.EnvironmentTesting,
                AgentProductIdentity.ServiceDescription);

            var archivePath = Path.Combine(options.AvailableRoot, manifest.PackageId + "-" + manifest.Version + ".zip");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(unsafeArchive ? "../escape.dll" : "RmsSupportAgent.exe");
                using var stream = entry.Open();
                stream.Write(fileBytes);
            }

            var archiveBytes = File.ReadAllBytes(archivePath);
            manifest = manifest with
            {
                PackageSha256 = Convert.ToHexString(SHA256.HashData(archiveBytes)).ToLowerInvariant(),
                PackageSizeBytes = archiveBytes.LongLength
            };
            var verifier = new FileAgentPackageVerifier(options, new RmsSupportHub.Pos.Application.Packages.AgentPackagePolicy(), new AcceptingSignatureVerifier());
            return new PackageFixture(root, options, manifest, verifier);
        }

        public FileAgentPackageLifecycle CreateLifecycle(IAgentPackageInstallationPlatform platform) => new(options, verifier, platform);

        public AgentPackageExecutionRequest Request(AgentPackageOperationKind operation) => new(operation, manifest, null, "S-1-5-18", Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        private sealed class AcceptingSignatureVerifier : IAgentPackageSignatureVerifier
        {
            public Task<bool> VerifyAsync(AgentPackageManifest manifest, string archivePath, CancellationToken cancellationToken = default) => Task.FromResult(true);
        }
    }
}
