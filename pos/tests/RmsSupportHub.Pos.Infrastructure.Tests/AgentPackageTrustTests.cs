using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RmsSupportHub.Pos.Application.Packages;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Packages;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

public sealed class AgentPackageTrustTests
{
    [Fact]
    public async Task CanonicalSignatureBindsIdentityFilesAndReleaseChannel()
    {
        using var certificate = CreateCodeSigningCertificate();
        var trust = new RecordingTrustValidator();
        var verifier = new MachineCertificatePackageSignatureVerifier(
            new AgentPackageTrustOptions
            {
                ProductionSignerThumbprint = new string('0', 40),
                TestingSignerThumbprint = certificate.Thumbprint,
                RequireTrustedChain = false
            },
            new FixedCertificateSource(certificate),
            trust);
        var manifest = Sign(CreateManifest(certificate), certificate);

        Assert.True(await verifier.VerifyAsync(manifest, "unused.zip"));
        Assert.False(trust.LastRequireTrustedChain);
        Assert.False(await verifier.VerifyAsync(manifest with { Version = "1.0.1" }, "unused.zip"));
        Assert.False(await verifier.VerifyAsync(manifest with { ProductId = "OtherProduct" }, "unused.zip"));
        Assert.False(await verifier.VerifyAsync(manifest with { ReleaseChannel = "Production", Environment = "Production" }, "unused.zip"));
        Assert.False(await verifier.VerifyAsync(manifest with { Files = [manifest.Files[0] with { Sha256 = new string('c', 64) }] }, "unused.zip"));
    }

    [Fact]
    public async Task ProductionCannotUseTheTestingChainBypassOrTestingSignerPin()
    {
        using var certificate = CreateCodeSigningCertificate();
        Assert.Throws<ArgumentException>(() => new AgentPackageTrustOptions
        {
            ProductionSignerThumbprint = certificate.Thumbprint,
            TestingSignerThumbprint = certificate.Thumbprint,
            RequireTrustedChain = false
        }.Validate());

        var strictVerifier = new MachineCertificatePackageSignatureVerifier(
            new AgentPackageTrustOptions
            {
                ProductionSignerThumbprint = certificate.Thumbprint,
                TestingSignerThumbprint = new string('0', 40),
                RequireTrustedChain = false
            },
            new FixedCertificateSource(certificate),
            new X509ChainAgentPackageSignerTrustValidator());
        var production = Sign(CreateManifest(certificate) with { ReleaseChannel = "Production", Environment = "Production" }, certificate);
        Assert.False(await strictVerifier.VerifyAsync(production, "unused.zip"));

        var noProductionPinVerifier = new MachineCertificatePackageSignatureVerifier(
            new AgentPackageTrustOptions
            {
                ProductionSignerThumbprint = new string('0', 40),
                TestingSignerThumbprint = certificate.Thumbprint,
                RequireTrustedChain = false
            },
            new FixedCertificateSource(certificate),
            new RecordingTrustValidator());
        Assert.False(await noProductionPinVerifier.VerifyAsync(production, "unused.zip"));
    }

    [Theory]
    [InlineData("AABBCCDDEEFF00112233445566778899AABBCCDD", "aabbccddee ff00112233445566778899aabbccdd")]
    [InlineData("AABBCCDDEEFF00112233445566778899AABBCCDD", " aabb ccdd eeff 0011 2233 4455 6677 8899 aabb ccdd ")]
    public void RejectsEqualSignerPinsAfterCanonicalNormalization(string production, string testing)
    {
        Assert.Throws<ArgumentException>(() => new AgentPackageTrustOptions
        {
            ProductionSignerThumbprint = production,
            TestingSignerThumbprint = testing
        }.Validate());
    }

    [Fact]
    public async Task ProductionUsesItsDistinctPinnedSignerAndCannotUseTestingPinnedSigner()
    {
        using var productionCertificate = CreateCodeSigningCertificate();
        using var testingCertificate = CreateCodeSigningCertificate();
        var trust = new RecordingTrustValidator();
        var verifier = new MachineCertificatePackageSignatureVerifier(
            new AgentPackageTrustOptions
            {
                ProductionSignerThumbprint = productionCertificate.Thumbprint,
                TestingSignerThumbprint = testingCertificate.Thumbprint,
                RequireTrustedChain = false
            },
            new DictionaryCertificateSource(productionCertificate, testingCertificate),
            trust);

        var production = Sign(CreateManifest(productionCertificate) with { ReleaseChannel = "Production", Environment = "Production" }, productionCertificate);
        var testingSignedByProduction = Sign(CreateManifest(productionCertificate), productionCertificate);
        var productionSignedByTesting = Sign(
            CreateManifest(testingCertificate) with { ReleaseChannel = "Production", Environment = "Production" },
            testingCertificate);

        Assert.True(await verifier.VerifyAsync(production, "unused.zip"));
        Assert.True(trust.LastRequireTrustedChain);
        Assert.False(await verifier.VerifyAsync(testingSignedByProduction, "unused.zip"));
        Assert.False(await verifier.VerifyAsync(productionSignedByTesting, "unused.zip"));
    }

    [Fact]
    public void VerifierExposesNoPublicConstructionPathThatIndependentlyReloadsMachineTrust()
    {
        // The verifier's only runtime trust authority is the immutable snapshot the host resolves
        // once at startup. A public parameterless (or otherwise machine-trust-reloading) constructor
        // would let any caller bypass that snapshot and re-read package-trust.json after startup, so
        // none may exist.
        var publicConstructors = typeof(MachineCertificatePackageSignatureVerifier)
            .GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        Assert.All(publicConstructors, constructor => Assert.NotEmpty(constructor.GetParameters()));
        Assert.All(
            publicConstructors,
            constructor => Assert.Contains(
                constructor.GetParameters(),
                parameter => parameter.ParameterType == typeof(AgentMachineTrustConfiguration)));
    }

    [Fact]
    public void InstallationPlatformExposesNoPublicConstructionPathThatIndependentlyReloadsMachineTrust()
    {
        // The Windows activation platform must always receive its verifier from the caller (DI in
        // production, explicit fixtures in tests); it must never be able to build its own
        // machine-trust-reloading verifier internally.
        var publicConstructors = typeof(WindowsAgentPackageInstallationPlatform)
            .GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        Assert.All(publicConstructors, constructor => Assert.NotEmpty(constructor.GetParameters()));
    }

    [Fact]
    public void CanonicalizationIsIndependentOfJsonOrFileEnumerationOrder()
    {
        using var certificate = CreateCodeSigningCertificate();
        var first = CreateManifest(certificate);
        var second = first with { Files = first.Files.Reverse().ToArray(), AclRequirements = first.AclRequirements.Reverse().ToArray() };

        Assert.Equal(AgentPackageCanonicalizer.Canonicalize(first), AgentPackageCanonicalizer.Canonicalize(second));
        Assert.NotEqual(
            AgentPackageCanonicalizer.Canonicalize(first),
            AgentPackageCanonicalizer.Canonicalize(first with { CertificateRequirements = ["OtherRequirement"] }));
    }

    private static AgentPackageManifest Sign(AgentPackageManifest manifest, X509Certificate2 certificate)
    {
        using var rsa = certificate.GetRSAPrivateKey()!;
        var signature = rsa.SignData(AgentPackageCanonicalizer.Canonicalize(manifest), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return manifest with { Signature = Convert.ToBase64String(signature) };
    }

    private static AgentPackageManifest CreateManifest(X509Certificate2 certificate)
    {
        return new AgentPackageManifest(
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

    private sealed class DictionaryCertificateSource(params X509Certificate2[] certificates) : IAgentPackageSignerCertificateSource
    {
        public X509Certificate2? Find(string thumbprint) => certificates
            .FirstOrDefault(certificate => string.Equals(certificate.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase)) is { } certificate
            ? new(certificate)
            : null;
    }

    private sealed class RecordingTrustValidator : IAgentPackageSignerTrustValidator
    {
        public bool WasCalled { get; private set; }

        public bool LastRequireTrustedChain { get; private set; }

        public bool IsTrusted(X509Certificate2 certificate, bool requireTrustedChain, out string failureCode)
        {
            WasCalled = true;
            LastRequireTrustedChain = requireTrustedChain;
            failureCode = "test";
            return true;
        }
    }
}
