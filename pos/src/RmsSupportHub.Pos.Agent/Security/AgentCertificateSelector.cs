using System.Formats.Asn1;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RmsSupportHub.Pos.Agent.Security;

public sealed record CertificateValidationResult(bool IsValid, string? FailureReason)
{
    public static CertificateValidationResult Valid { get; } = new(true, null);
}

/// <summary>
/// Selects only a deployment-owned LocalMachine certificate that has a private key, is currently
/// valid, and contains the exact Agent DNS SAN. No file certificate or development fallback is used.
/// </summary>
public static class AgentCertificateSelector
{
    private const string SubjectAlternativeNameOid = "2.5.29.17";

    public static X509Certificate2? FindFromLocalMachine(string? thumbprint, DateTimeOffset nowUtc)
    {
        var normalizedThumbprint = NormalizeThumbprint(thumbprint);
        if (normalizedThumbprint is null)
        {
            return null;
        }

        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            return SelectFromCertificates(store.Certificates, normalizedThumbprint, nowUtc);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (SecurityException)
        {
            return null;
        }
    }

    public static X509Certificate2? SelectFromCertificates(
        IEnumerable<X509Certificate2> certificates,
        string? thumbprint,
        DateTimeOffset nowUtc)
    {
        var normalizedThumbprint = NormalizeThumbprint(thumbprint);
        if (normalizedThumbprint is null)
        {
            return null;
        }

        foreach (var certificate in certificates)
        {
            if (!string.Equals(NormalizeThumbprint(certificate.Thumbprint), normalizedThumbprint, StringComparison.Ordinal)
                || !Validate(certificate, nowUtc).IsValid)
            {
                continue;
            }

            return certificate;
        }

        return null;
    }

    public static CertificateValidationResult Validate(X509Certificate2? certificate, DateTimeOffset nowUtc)
    {
        if (certificate is null)
        {
            return new(false, "certificate_missing");
        }

        if (!certificate.HasPrivateKey)
        {
            return new(false, "private_key_missing");
        }

        var now = nowUtc.UtcDateTime;
        if (certificate.NotBefore.ToUniversalTime() > now || certificate.NotAfter.ToUniversalTime() < now)
        {
            return new(false, "certificate_expired_or_not_yet_valid");
        }

        if (!HasExactDnsSan(certificate, AgentHostConstants.CanonicalHost))
        {
            return new(false, "canonical_dns_san_missing");
        }

        return CertificateValidationResult.Valid;
    }

    public static bool HasExactDnsSan(X509Certificate2 certificate, string expectedDnsName)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedDnsName);

        foreach (var extension in certificate.Extensions)
        {
            if (!string.Equals(extension.Oid?.Value, SubjectAlternativeNameOid, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var reader = new AsnReader(extension.RawData, AsnEncodingRules.DER);
                var sequence = reader.ReadSequence();
                while (sequence.HasData)
                {
                    var tag = sequence.PeekTag();
                    if (tag.TagClass == TagClass.ContextSpecific && tag.TagValue == 2)
                    {
                        var dnsName = sequence.ReadCharacterString(UniversalTagNumber.IA5String, tag);
                        if (string.Equals(dnsName, expectedDnsName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        _ = sequence.ReadEncodedValue();
                    }
                }

                if (!reader.HasData)
                {
                    continue;
                }
            }
            catch (AsnContentException)
            {
                return false;
            }
        }

        return false;
    }

    private static string? NormalizeThumbprint(string? thumbprint)
    {
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            return null;
        }

        var normalized = new string(thumbprint.Where(character => !char.IsWhiteSpace(character)).ToArray()).ToUpperInvariant();
        return normalized.Length == 40 && normalized.All(Uri.IsHexDigit) ? normalized : null;
    }
}
