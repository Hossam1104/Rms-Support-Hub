using System.Formats.Asn1;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using RmsSupportHub.Pos.Application.Packages;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Packages;

public sealed class AgentCertificatePrerequisiteOptions
{
    public string? Thumbprint { get; init; }

    public string ConfigurationPath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "DBS",
        "RmsSupportAgent",
        "Trust",
        "agent-certificate.json");

    public void Validate()
    {
        if (!string.IsNullOrWhiteSpace(ConfigurationPath)
            && (!Path.IsPathFullyQualified(ConfigurationPath)
                || ConfigurationPath.Any(char.IsControl)
                || ConfigurationPath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or "..")))
        {
            throw new ArgumentException("The Agent certificate configuration path must be fixed and absolute.", nameof(ConfigurationPath));
        }

        if (AgentPackageTrustOptions.Normalize(Thumbprint) is null && !string.IsNullOrWhiteSpace(Thumbprint))
        {
            throw new ArgumentException("The Agent certificate thumbprint is invalid.", nameof(Thumbprint));
        }
    }
}

public interface IAgentCertificatePrerequisite
{
    bool IsSatisfied(AgentPackageManifest manifest, out string failureCode);
}

/// <summary>
/// Reads only the deployment-selected LocalMachine certificate. It does not issue, import, bind,
/// replace, or remove certificates; enterprise-owned certificates remain enterprise-owned.
/// </summary>
public sealed class MachineAgentCertificatePrerequisite : IAgentCertificatePrerequisite
{
    private const string SubjectAlternativeNameOid = "2.5.29.17";
    private const string ServerAuthenticationEku = "1.3.6.1.5.5.7.3.1";
    public const string OwnershipMarkerOid = "1.3.6.1.4.1.55555.1.1";

    private readonly AgentCertificatePrerequisiteOptions options;

    public MachineAgentCertificatePrerequisite()
        : this(new AgentCertificatePrerequisiteOptions())
    {
    }

    public MachineAgentCertificatePrerequisite(AgentCertificatePrerequisiteOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
    }

    public bool IsSatisfied(AgentPackageManifest manifest, out string failureCode)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        failureCode = "certificate_missing";
        var thumbprint = ResolveThumbprint();
        if (thumbprint is null)
        {
            failureCode = "certificate_thumbprint_unconfigured";
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            failureCode = "certificate_local_machine_store_unavailable";
            return false;
        }

        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            var certificate = store.Certificates
                .Cast<X509Certificate2>()
                .FirstOrDefault(item => string.Equals(AgentPackageTrustOptions.Normalize(item.Thumbprint), thumbprint, StringComparison.Ordinal));
            if (certificate is null)
            {
                failureCode = "certificate_not_found";
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            var evidence = ToEvidence(certificate);
            var assessment = new AgentCertificatePolicy().Assess(evidence, now);
            if (!assessment.Valid)
            {
                failureCode = assessment.Code;
                return false;
            }

            if (!HasExactDnsSan(certificate, "rms-pos-agent.localhost"))
            {
                failureCode = "certificate_san_invalid";
                return false;
            }

            if (!HasServerAuthenticationEku(certificate))
            {
                failureCode = "server_authentication_eku_missing";
                return false;
            }

            if (!HasLocalSystemPrivateKeyAccess(certificate))
            {
                failureCode = "local_system_private_key_access_unproven";
                return false;
            }

            failureCode = "certificate_ready";
            return true;
        }
        catch (CryptographicException)
        {
            failureCode = "certificate_prerequisite_unknown";
            return false;
        }
        catch (SecurityException)
        {
            failureCode = "certificate_store_access_denied";
            return false;
        }
        catch (IOException)
        {
            failureCode = "certificate_store_unavailable";
            return false;
        }
    }

    private string? ResolveThumbprint()
    {
        var direct = AgentPackageTrustOptions.Normalize(options.Thumbprint);
        if (direct is not null) return direct;
        if (string.IsNullOrWhiteSpace(options.ConfigurationPath)) return null;

        try
        {
            if (!File.Exists(options.ConfigurationPath)
                || File.GetAttributes(options.ConfigurationPath).HasFlag(FileAttributes.ReparsePoint)
                || new FileInfo(options.ConfigurationPath).Length is <= 0 or > 8 * 1024)
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(options.ConfigurationPath));
            return document.RootElement.TryGetProperty("certificateThumbprint", out var value)
                ? AgentPackageTrustOptions.Normalize(value.GetString())
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static AgentCertificateEvidence ToEvidence(X509Certificate2 certificate) =>
        new(
            certificate.Thumbprint,
            HasExactDnsSan(certificate, "rms-pos-agent.localhost") ? "rms-pos-agent.localhost" : null,
            certificate.HasPrivateKey,
            GetProvider(certificate),
            IsExportable(certificate),
            HasServerAuthenticationEku(certificate),
            GetOwnershipMarker(certificate),
            certificate.NotBefore.ToUniversalTime(),
            certificate.NotAfter.ToUniversalTime(),
            "LocalMachine");

    private static string? GetProvider(X509Certificate2 certificate)
    {
        try
        {
            using var rsa = certificate.GetRSAPrivateKey();
            if (rsa is not RSACng cng) return null;
            var key = cng.Key;
            return key is null ? null : key.Provider?.Provider;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsExportable(X509Certificate2 certificate)
    {
        try
        {
            using var rsa = certificate.GetRSAPrivateKey();
            return rsa is RSACng cng && cng.Key.ExportPolicy.HasFlag(CngExportPolicies.AllowExport);
        }
        catch
        {
            return true;
        }
    }

    private static string? GetOwnershipMarker(X509Certificate2 certificate)
    {
        var extension = certificate.Extensions.FirstOrDefault(item => string.Equals(item.Oid?.Value, OwnershipMarkerOid, StringComparison.Ordinal));
        if (extension is null) return null;
        try
        {
            if (extension.RawData.Length > 256) return null;
            return System.Text.Encoding.UTF8.GetString(extension.RawData);
        }
        catch
        {
            return null;
        }
    }

    private static bool HasLocalSystemPrivateKeyAccess(X509Certificate2 certificate)
    {
        try
        {
            using var rsa = certificate.GetRSAPrivateKey();
            if (rsa is not RSACng cng) return false;
            var key = cng.Key;
            _ = key?.Provider?.Provider ?? throw new CryptographicException("The CNG provider is unavailable.");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasServerAuthenticationEku(X509Certificate2 certificate) =>
        certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .Any(extension => extension.EnhancedKeyUsages.Cast<Oid>().Any(usage => string.Equals(usage.Value, ServerAuthenticationEku, StringComparison.Ordinal)));

    private static bool HasExactDnsSan(X509Certificate2 certificate, string expectedDnsName)
    {
        foreach (var extension in certificate.Extensions.Where(item => string.Equals(item.Oid?.Value, SubjectAlternativeNameOid, StringComparison.Ordinal)))
        {
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
                        if (string.Equals(dnsName, expectedDnsName, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    else
                    {
                        _ = sequence.ReadEncodedValue();
                    }
                }
            }
            catch (AsnContentException)
            {
                return false;
            }
        }

        return false;
    }
}
