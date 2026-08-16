using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.Json;
using RmsSupportHub.Pos.Application.Packages;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Configuration;
using RmsSupportHub.Pos.Infrastructure.Packages;
using RmsSupportHub.Pos.Infrastructure.Windows;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

/// <summary>
/// Exercises the real <see cref="WindowsAgentPackageInstallationPlatform"/> rollback/recovery seam
/// end to end -- real cryptographic signing/verification, real archive extraction and staged-hash
/// checks, real ACL-boundary enforcement -- with only the Windows SCM, certificate prerequisite, and
/// HTTPS health probe replaced by safe injected fakes. <see cref="AgentPackageLifecycleTests"/>
/// already proves orchestration against a recording fake platform; these tests prove the platform
/// itself does not trust rollback bytes that are not bound to a currently verified signed manifest.
/// </summary>
public sealed class AgentPackageWindowsRollbackTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task FailedUpgradeAutomaticRollbackRestoresPreviousVersionUsingRetainedManifestNotIncoming()
    {
        using var fixture = RealPlatformFixture.Create();
        var v1 = fixture.PublishAvailable("1.0.0", 1);
        var install = await fixture.Lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Install, v1));
        Assert.True(install.State == AgentPackageOperationState.Completed, $"{install.State}: {install.Detail}");

        var v2 = fixture.PublishAvailable("2.0.0", 2, "1.0.0");
        fixture.HealthProbe.Enqueue(false);
        fixture.HealthProbe.DefaultResult = true;

        var upgrade = await fixture.Lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Upgrade, v2));

        Assert.True(upgrade.State == AgentPackageOperationState.RollbackSucceeded, $"{upgrade.State}: {upgrade.Detail}");
        Assert.True(upgrade.RollbackAttempted);
        Assert.True(upgrade.RollbackSucceeded);
        Assert.False(upgrade.RecoveryRequired);

        var restored = fixture.ReadInstalledManifest();
        Assert.NotNull(restored);
        Assert.Equal("1.0.0", restored!.Version);
        Assert.True(fixture.HealthProbe.ProbeCount >= 2, "the restored version's own health must be verified, not assumed");
        Assert.Null(fixture.StateStore.Read());
    }

    [Fact]
    public async Task AutomaticRollbackFailsClosedWhenRestoredVersionHealthDoesNotConfirm()
    {
        using var fixture = RealPlatformFixture.Create();
        var v1 = fixture.PublishAvailable("1.0.0", 1);
        var install = await fixture.Lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Install, v1));
        Assert.Equal(AgentPackageOperationState.Completed, install.State);

        var v2 = fixture.PublishAvailable("2.0.0", 2, "1.0.0");
        fixture.HealthProbe.Enqueue(false);
        fixture.HealthProbe.DefaultResult = false;

        var upgrade = await fixture.Lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Upgrade, v2));

        Assert.Equal(AgentPackageOperationState.RollbackFailed, upgrade.State);
        Assert.True(upgrade.RollbackAttempted);
        Assert.False(upgrade.RollbackSucceeded);
        Assert.True(upgrade.RecoveryRequired);

        var checkpoint = fixture.StateStore.Read();
        Assert.NotNull(checkpoint);
        Assert.True(checkpoint!.RecoveryRequired);
        Assert.Equal(AgentPackageLifecyclePhase.RecoveryRequired, checkpoint.Phase);
    }

    [Fact]
    public async Task AutomaticRollbackRejectsATamperedRetainedArchiveWithoutActivatingIt()
    {
        using var fixture = RealPlatformFixture.Create();
        var v1 = fixture.PublishAvailable("1.0.0", 1);
        var install = await fixture.Lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Install, v1));
        Assert.Equal(AgentPackageOperationState.Completed, install.State);

        var v2 = fixture.PublishAvailable("2.0.0", 2, "1.0.0");
        var upgrade = await fixture.Lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Upgrade, v2));
        Assert.Equal(AgentPackageOperationState.Completed, upgrade.State);

        fixture.CorruptRetainedArchive(fixture.Options.RollbackRoot, v1);
        fixture.WriteCheckpoint("1.0.0");

        var rolledBack = await fixture.Platform.RollbackAsync(AgentPackageOperationKind.Upgrade, CancellationToken.None);

        Assert.False(rolledBack);
        Assert.NotEqual("none", fixture.Platform.LastFailureCode);
        var installed = fixture.ReadInstalledManifest();
        Assert.Equal("2.0.0", installed!.Version);
    }

    [Fact]
    public async Task AutomaticRollbackRejectsARetainedManifestWithATamperedFieldAndAStaleSignature()
    {
        using var fixture = RealPlatformFixture.Create();
        var v1 = fixture.PublishAvailable("1.0.0", 1);
        var install = await fixture.Lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Install, v1));
        Assert.Equal(AgentPackageOperationState.Completed, install.State);

        var v2 = fixture.PublishAvailable("2.0.0", 2, "1.0.0");
        var upgrade = await fixture.Lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Upgrade, v2));
        Assert.Equal(AgentPackageOperationState.Completed, upgrade.State);

        var tampered = v1 with { SignerDisplayName = "Tampered Signer" };
        fixture.WriteManifestToSlot(fixture.Options.RollbackRoot, tampered);
        fixture.WriteCheckpoint("1.0.0");

        var rolledBack = await fixture.Platform.RollbackAsync(AgentPackageOperationKind.Upgrade, CancellationToken.None);

        Assert.False(rolledBack);
        var installed = fixture.ReadInstalledManifest();
        Assert.Equal("2.0.0", installed!.Version);
    }

    [Fact]
    public async Task AutomaticRollbackRejectsARetainedManifestSignedByAnUntrustedSigner()
    {
        using var fixture = RealPlatformFixture.Create();
        var v1 = fixture.PublishAvailable("1.0.0", 1);
        var install = await fixture.Lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Install, v1));
        Assert.Equal(AgentPackageOperationState.Completed, install.State);

        var v2 = fixture.PublishAvailable("2.0.0", 2, "1.0.0");
        var upgrade = await fixture.Lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Upgrade, v2));
        Assert.Equal(AgentPackageOperationState.Completed, upgrade.State);

        using var otherSigner = CreateCodeSigningCertificate();
        var wronglySigned = Sign(v1 with { Signature = string.Empty }, otherSigner);
        fixture.WriteManifestToSlot(fixture.Options.RollbackRoot, wronglySigned);
        fixture.WriteCheckpoint("1.0.0");

        var rolledBack = await fixture.Platform.RollbackAsync(AgentPackageOperationKind.Upgrade, CancellationToken.None);

        Assert.False(rolledBack);
        var installed = fixture.ReadInstalledManifest();
        Assert.Equal("2.0.0", installed!.Version);
    }

    [Fact]
    public async Task ExplicitRollbackFailurePreservesAndRestoresTheCurrentVersion()
    {
        using var fixture = RealPlatformFixture.Create();
        var v1 = fixture.PublishAvailable("1.0.0", 1);
        var install = await fixture.Lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Install, v1));
        Assert.Equal(AgentPackageOperationState.Completed, install.State);

        var v2 = fixture.PublishAvailable("2.0.0", 2, "1.0.0");
        var upgrade = await fixture.Lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Upgrade, v2));
        Assert.Equal(AgentPackageOperationState.Completed, upgrade.State);

        fixture.HealthProbe.Enqueue(false);
        fixture.HealthProbe.DefaultResult = true;

        var explicitRollback = await fixture.Lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Rollback, v1));

        Assert.True(explicitRollback.State == AgentPackageOperationState.RollbackSucceeded, $"{explicitRollback.State}: {explicitRollback.Detail}");
        Assert.True(explicitRollback.RollbackAttempted);
        Assert.True(explicitRollback.RollbackSucceeded);

        var installed = fixture.ReadInstalledManifest();
        Assert.Equal("2.0.0", installed!.Version);
        Assert.Null(fixture.StateStore.Read());
    }

    [Fact]
    public async Task FreshInstallActivationFailureNeverFalselyReportsRollbackSuccess()
    {
        using var fixture = RealPlatformFixture.Create();
        var v1 = fixture.PublishAvailable("1.0.0", 1);
        fixture.HealthProbe.Enqueue(false);

        var install = await fixture.Lifecycle.ExecuteAsync(fixture.Request(AgentPackageOperationKind.Install, v1));

        Assert.Equal(AgentPackageOperationState.RollbackFailed, install.State);
        Assert.True(install.RollbackAttempted);
        Assert.False(install.RollbackSucceeded);
        Assert.True(install.RecoveryRequired);
    }

    [Fact]
    public void TrustControlFileWithAnUnprotectedInheritedAclFailsClosed()
    {
        var root = Path.Combine(Path.GetTempPath(), "RmsSupportHub.Pos.Tests", "rms-agent-acl-unprotected-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "package-trust.json");
            File.WriteAllText(path, "{}");

            Assert.False(ServiceOwnedDirectoryProvisioner.IsTrustedControlFile(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TrustControlFileWithABroadEveryoneGrantFailsClosed()
    {
        var root = Path.Combine(Path.GetTempPath(), "RmsSupportHub.Pos.Tests", "rms-agent-acl-everyone-" + Guid.NewGuid().ToString("N"));
        try
        {
            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(root);
            var path = Path.Combine(root, "package-trust.json");
            File.WriteAllText(path, "{}");
            ServiceOwnedDirectoryProvisioner.EnsureControlFileAcl(path);

            var security = new FileInfo(path).GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                FileSystemRights.Modify,
                InheritanceFlags.None,
                PropagationFlags.None,
                AccessControlType.Allow));
            new FileInfo(path).SetAccessControl(security);

            Assert.False(ServiceOwnedDirectoryProvisioner.IsTrustedControlFile(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TrustControlFileWithARestrictedOwnedAclSucceeds()
    {
        var root = Path.Combine(Path.GetTempPath(), "RmsSupportHub.Pos.Tests", "rms-agent-acl-trusted-" + Guid.NewGuid().ToString("N"));
        try
        {
            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(root);
            var path = Path.Combine(root, "package-trust.json");
            File.WriteAllText(path, "{}");
            ServiceOwnedDirectoryProvisioner.EnsureControlFileAcl(path);

            Assert.True(ServiceOwnedDirectoryProvisioner.IsTrustedControlFile(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PackageSignatureVerifierFailsClosedWhenTrustConfigurationHasAnUnsafeAcl()
    {
        var root = Path.Combine(Path.GetTempPath(), "RmsSupportHub.Pos.Tests", "rms-agent-trust-config-" + Guid.NewGuid().ToString("N"));
        try
        {
            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(root);
            var configPath = Path.Combine(root, "package-trust.json");
            using var certificate = CreateCodeSigningCertificate();
            File.WriteAllText(configPath, $$"""{"testingSignerThumbprint":"{{certificate.Thumbprint}}"}""");
            ServiceOwnedDirectoryProvisioner.EnsureControlFileAcl(configPath);
            var security = new FileInfo(configPath).GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                FileSystemRights.Read,
                AccessControlType.Allow));
            new FileInfo(configPath).SetAccessControl(security);

            var verifier = new MachineCertificatePackageSignatureVerifier(
                new AgentPackageTrustOptions { TrustConfigurationPath = configPath, RequireTrustedChain = false },
                new FixedCertificateSource(certificate),
                new AcceptingTrustValidator());

            var manifest = Sign(CreateBareManifest(), certificate);

            Assert.False(await verifier.VerifyAsync(manifest, "unused.zip"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static AgentPackageManifest CreateBareManifest() =>
        new(
            "rms-support-agent",
            "1.0.0",
            "Windows",
            "net10.0-windows",
            AgentProductIdentity.ServiceDisplayName,
            "LocalSystem",
            AgentProductIdentity.PermanentServiceName,
            "SHA256withRSA",
            "DBS test signer",
            new string('a', 64),
            string.Empty,
            [new AgentPackageFileManifest("agent", "RmsSupportAgent.exe", 1, new string('b', 64), true)],
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

    private static AgentPackageManifest Sign(AgentPackageManifest manifest, X509Certificate2 certificate)
    {
        using var rsa = certificate.GetRSAPrivateKey()!;
        var signature = rsa.SignData(AgentPackageCanonicalizer.Canonicalize(manifest), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return manifest with { Signature = Convert.ToBase64String(signature) };
    }

    private static X509Certificate2 CreateCodeSigningCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=RMS Test Code Signer",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.3") },
            true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
    }

    private sealed class FixedCertificateSource(X509Certificate2 certificate) : IAgentPackageSignerCertificateSource
    {
        public X509Certificate2? Find(string thumbprint) => new(certificate);
    }

    private sealed class AcceptingTrustValidator : IAgentPackageSignerTrustValidator
    {
        public bool IsTrusted(X509Certificate2 certificate, bool requireTrustedChain, out string failureCode)
        {
            failureCode = "test";
            return true;
        }
    }

    private sealed class FakeServiceController : IAgentServiceLifecycleController
    {
        private string? configuredExecutablePath;

        public ServiceStatus Status { get; set; } = ServiceStatus.NotFound;

        public AgentServiceEvidence? ReadPermanent()
        {
            if (configuredExecutablePath is null) return null;
            return new AgentServiceEvidence(
                AgentProductIdentity.PermanentServiceName,
                AgentProductIdentity.ServiceDisplayName,
                AgentProductIdentity.ServiceDescription,
                configuredExecutablePath,
                null,
                null,
                null,
                null,
                AgentResourceOwnershipState.Owned)
            {
                ServiceAccount = "LocalSystem",
                StartType = "Auto",
                HasArguments = false
            };
        }

        public bool EnsureConfigured(string executablePath)
        {
            configuredExecutablePath = executablePath;
            return true;
        }

        public bool Stop(CancellationToken cancellationToken = default)
        {
            Status = ServiceStatus.Stopped;
            return true;
        }

        public bool Start(CancellationToken cancellationToken = default)
        {
            Status = ServiceStatus.Running;
            return true;
        }

        public bool Delete(CancellationToken cancellationToken = default)
        {
            configuredExecutablePath = null;
            Status = ServiceStatus.NotFound;
            return true;
        }

        public ServiceStatus GetStatus() => Status;
    }

    private sealed class FakeCertificatePrerequisite : IAgentCertificatePrerequisite
    {
        public bool IsSatisfied(AgentPackageManifest manifest, out string failureCode)
        {
            failureCode = "certificate_ready";
            return true;
        }
    }

    private sealed class FakeHealthProbe : IAgentHealthProbe
    {
        private readonly Queue<bool> queued = new();

        public bool DefaultResult { get; set; } = true;

        public int ProbeCount { get; private set; }

        public void Enqueue(bool result) => queued.Enqueue(result);

        public Task<bool> ProbeAsync(CancellationToken cancellationToken = default)
        {
            ProbeCount++;
            return Task.FromResult(queued.Count > 0 ? queued.Dequeue() : DefaultResult);
        }
    }

    private sealed class RealPlatformFixture : IDisposable
    {
        private readonly string root;
        private readonly X509Certificate2 certificate;

        private RealPlatformFixture(
            string root,
            AgentPackageOptions options,
            FileAgentPackageVerifier verifier,
            FakeHealthProbe healthProbe,
            AgentPackageLifecycleStateStore stateStore,
            WindowsAgentPackageInstallationPlatform platform,
            FileAgentPackageLifecycle lifecycle,
            X509Certificate2 certificate)
        {
            this.root = root;
            Options = options;
            Verifier = verifier;
            HealthProbe = healthProbe;
            StateStore = stateStore;
            Platform = platform;
            Lifecycle = lifecycle;
            this.certificate = certificate;
        }

        public AgentPackageOptions Options { get; }

        public FileAgentPackageVerifier Verifier { get; }

        public FakeHealthProbe HealthProbe { get; }

        public AgentPackageLifecycleStateStore StateStore { get; }

        public WindowsAgentPackageInstallationPlatform Platform { get; }

        public FileAgentPackageLifecycle Lifecycle { get; }

        public static RealPlatformFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "RmsSupportHub.Pos.Tests", "rms-agent-real-platform-" + Guid.NewGuid().ToString("N"));
            var options = new AgentPackageOptions
            {
                PackageRoot = Path.Combine(root, "packages"),
                InstallationRoot = Path.Combine(root, "install"),
                ReleaseChannel = AgentProductIdentity.ReleaseChannelTesting
            };
            options.EnsureStorageProvisioned();

            var certificate = CreateCodeSigningCertificate();
            var signatureVerifier = new MachineCertificatePackageSignatureVerifier(
                new AgentPackageTrustOptions { TestingSignerThumbprint = certificate.Thumbprint, RequireTrustedChain = false },
                new FixedCertificateSource(certificate),
                new AcceptingTrustValidator());
            var verifier = new FileAgentPackageVerifier(options, new AgentPackagePolicy(), signatureVerifier);
            var stateStore = new AgentPackageLifecycleStateStore(options);
            var serviceController = new FakeServiceController();
            var certificatePrerequisite = new FakeCertificatePrerequisite();
            var healthProbe = new FakeHealthProbe();
            var platform = new WindowsAgentPackageInstallationPlatform(options, verifier, serviceController, certificatePrerequisite, stateStore, healthProbe);
            var lifecycle = new FileAgentPackageLifecycle(options, verifier, platform);
            return new RealPlatformFixture(root, options, verifier, healthProbe, stateStore, platform, lifecycle, certificate);
        }

        public AgentPackageManifest PublishAvailable(string version, byte fileByte, string? previousVersion = null)
        {
            var fileBytes = new byte[] { fileByte, fileByte, fileByte, fileByte };
            var manifest = new AgentPackageManifest(
                "rms-support-agent",
                version,
                "Windows",
                "net10.0-windows",
                AgentProductIdentity.ServiceDisplayName,
                "LocalSystem",
                AgentProductIdentity.PermanentServiceName,
                "SHA256withRSA",
                "DBS test signer",
                new string('0', 64),
                string.Empty,
                [new AgentPackageFileManifest("agent", "RmsSupportAgent.exe", fileBytes.Length, Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant(), true)],
                ["AgentServiceReadWrite"],
                ["AgentHttpsCertificate"],
                previousVersion,
                previousVersion is not null,
                fileBytes.Length,
                1,
                AgentProductIdentity.ProductId,
                "x64",
                AgentProductIdentity.ReleaseChannelTesting,
                AgentProductIdentity.EnvironmentTesting,
                AgentProductIdentity.ServiceDescription);

            var archiveBytes = BuildArchive(fileBytes);
            manifest = manifest with
            {
                PackageSha256 = Convert.ToHexString(SHA256.HashData(archiveBytes)).ToLowerInvariant(),
                PackageSizeBytes = archiveBytes.LongLength
            };
            manifest = Sign(manifest, certificate);

            var archivePath = Path.Combine(Options.AvailableRoot, manifest.PackageId + "-" + manifest.Version + ".zip");
            File.WriteAllBytes(archivePath, archiveBytes);
            return manifest;
        }

        public void WriteManifestToSlot(string slotRoot, AgentPackageManifest manifest)
        {
            Directory.CreateDirectory(slotRoot);
            File.WriteAllText(Path.Combine(slotRoot, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions));
        }

        public void CorruptRetainedArchive(string slotRoot, AgentPackageManifest manifest)
        {
            var archivePath = Path.Combine(slotRoot, manifest.PackageId + "-" + manifest.Version + ".zip");
            var tamperedBytes = BuildArchive([9, 9, 9, 9]);
            File.WriteAllBytes(archivePath, tamperedBytes);
        }

        public void WriteCheckpoint(string previousVersion)
        {
            var now = DateTimeOffset.UtcNow;
            StateStore.Write(new AgentPackageLifecycleState(
                Guid.NewGuid().ToString("N"),
                AgentPackageOperationKind.Upgrade,
                AgentPackageLifecyclePhase.HealthChecking,
                "rms-support-agent",
                "2.0.0",
                previousVersion,
                now,
                now,
                null,
                false));
        }

        public AgentPackageManifest? ReadInstalledManifest()
        {
            var path = Path.Combine(Options.InstallationRoot, "manifest.json");
            return !File.Exists(path) ? null : JsonSerializer.Deserialize<AgentPackageManifest>(File.ReadAllText(path), JsonOptions);
        }

        public AgentPackageExecutionRequest Request(AgentPackageOperationKind operation, AgentPackageManifest manifest) =>
            new(operation, manifest, null, "S-1-5-18", Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            certificate.Dispose();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        private static byte[] BuildArchive(byte[] fileBytes)
        {
            using var memory = new MemoryStream();
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("RmsSupportAgent.exe");
                using var stream = entry.Open();
                stream.Write(fileBytes);
            }

            return memory.ToArray();
        }
    }
}
