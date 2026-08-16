namespace RmsSupportHub.Pos.Infrastructure.Packages;

/// <summary>
/// Machine-owned signer configuration. The package manifest may describe a signer for display,
/// but it can never supply or replace these pins. Production and Testing pins are deliberately
/// separate so a Testing certificate cannot satisfy a Production operation.
/// </summary>
public sealed class AgentPackageTrustOptions
{
    public string? ProductionSignerThumbprint { get; init; }

    public string? TestingSignerThumbprint { get; init; }

    public string TrustConfigurationPath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "DBS",
        "RmsSupportAgent",
        "Trust",
        "package-trust.json");

    public bool RequireTrustedChain { get; init; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TrustConfigurationPath)
            || !Path.IsPathFullyQualified(TrustConfigurationPath)
            || TrustConfigurationPath.Any(char.IsControl)
            || TrustConfigurationPath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("The package trust configuration path must be a fixed absolute path.", nameof(TrustConfigurationPath));
        }

        ValidateThumbprint(ProductionSignerThumbprint, nameof(ProductionSignerThumbprint));
        ValidateThumbprint(TestingSignerThumbprint, nameof(TestingSignerThumbprint));
    }

    public string? GetConfiguredThumbprint(string channel) => channel switch
    {
        "Production" => Normalize(ProductionSignerThumbprint),
        "Testing" => Normalize(TestingSignerThumbprint),
        _ => null
    };

    private static void ValidateThumbprint(string? value, string name)
    {
        if (value is not null && Normalize(value) is null)
        {
            throw new ArgumentException("A signer thumbprint must be exactly 40 hexadecimal characters.", name);
        }
    }

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray()).ToUpperInvariant();
        return normalized.Length == 40 && normalized.All(Uri.IsHexDigit) ? normalized : null;
    }
}

public interface IAgentPackageSignerCertificateSource
{
    System.Security.Cryptography.X509Certificates.X509Certificate2? Find(string thumbprint);
}

public interface IAgentPackageSignerTrustValidator
{
    bool IsTrusted(
        System.Security.Cryptography.X509Certificates.X509Certificate2 certificate,
        bool requireTrustedChain,
        out string failureCode);
}
