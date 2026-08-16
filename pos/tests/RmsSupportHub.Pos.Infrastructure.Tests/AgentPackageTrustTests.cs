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
        var trust = new RecordingTrustValidator();
        var verifier = new MachineCertificatePackageSignatureVerifier(
            new AgentPackageTrustOptions
            {
                ProductionSignerThumbprint = certificate.Thumbprint,
                TestingSignerThumbprint = certificate.Thumbprint,
                RequireTrustedChain = false
            },
            new FixedCertificateSource(certificate),
            trust);
        var production = Sign(CreateManifest(certificate) with { ReleaseChannel = "Production", Environment = "Production" }, certificate);

        Assert.True(await verifier.VerifyAsync(production, "unused.zip"));
        Assert.True(trust.WasCalled);
        Assert.True(trust.LastRequireTrustedChain);

        var strictVerifier = new MachineCertificatePackageSignatureVerifier(
            new AgentPackageTrustOptions
            {
                ProductionSignerThumbprint = certificate.Thumbprint,
                RequireTrustedChain = false
            },
            new FixedCertificateSource(certificate),
            new X509ChainAgentPackageSignerTrustValidator());
        Assert.False(await strictVerifier.VerifyAsync(production, "unused.zip"));
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
