using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RmsSupportHub.Pos.Agent.Security;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class CertificateSelectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MissingCertificate_FailsClosed()
    {
        Assert.False(AgentCertificateSelector.Validate(null, Now).IsValid);
        Assert.Null(AgentCertificateSelector.FindFromLocalMachine(null, Now));
    }

    [Fact]
    public void ExpiredCertificate_FailsClosed()
    {
        using var certificate = CreateCertificate("rms-pos-agent.localhost", Now.AddHours(-2), Now.AddHours(-1));

        var result = AgentCertificateSelector.Validate(certificate, Now);

        Assert.False(result.IsValid);
        Assert.Equal("certificate_expired_or_not_yet_valid", result.FailureReason);
    }

    [Fact]
    public void CertificateWithoutPrivateKey_FailsClosed()
    {
        using var source = CreateCertificate("rms-pos-agent.localhost", Now.AddMinutes(-5), Now.AddHours(1));
        using var publicOnly = X509CertificateLoader.LoadCertificate(source.Export(X509ContentType.Cert));

        var result = AgentCertificateSelector.Validate(publicOnly, Now);

        Assert.False(result.IsValid);
        Assert.Equal("private_key_missing", result.FailureReason);
    }

    [Fact]
    public void WrongDnsName_FailsClosed()
    {
        using var certificate = CreateCertificate("other.example.test", Now.AddMinutes(-5), Now.AddHours(1));

        var result = AgentCertificateSelector.Validate(certificate, Now);

        Assert.False(result.IsValid);
        Assert.Equal("canonical_dns_san_missing", result.FailureReason);
    }

    [Fact]
    public void CorrectSanAndPrivateKeyAreAccepted()
    {
        using var certificate = CreateCertificate("rms-pos-agent.localhost", Now.AddMinutes(-5), Now.AddHours(1));

        var result = AgentCertificateSelector.Validate(certificate, Now);
        using var selected = AgentCertificateSelector.SelectFromCertificates(
            [certificate], certificate.Thumbprint, Now);

        Assert.True(result.IsValid);
        Assert.NotNull(selected);
        Assert.True(AgentCertificateSelector.HasExactDnsSan(certificate, "rms-pos-agent.localhost"));
    }

    private static X509Certificate2 CreateCertificate(string dnsName, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=certificate-test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(dnsName);
        request.CertificateExtensions.Add(san.Build());
        return request.CreateSelfSigned(notBefore, notAfter);
    }
}
