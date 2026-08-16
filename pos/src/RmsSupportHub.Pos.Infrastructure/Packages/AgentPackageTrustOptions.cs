namespace RmsSupportHub.Pos.Infrastructure.Packages;

/// <summary>
/// Machine-owned signer configuration. The package manifest may describe a signer for display,
/// but it can never supply or replace these pins. Production and Testing pins are deliberately
/// separate so a Testing certificate cannot satisfy a Production operation.
/// </summary>
public sealed class AgentPackageTrustOptions
{
    public string ProductionSignerThumbprint { get; init; } = string.Empty;

    public string TestingSignerThumbprint { get; init; } = string.Empty;

    public bool RequireTrustedChain { get; init; } = true;

    public void Validate()
    {
        var production = ValidateThumbprint(ProductionSignerThumbprint, nameof(ProductionSignerThumbprint));
        var testing = ValidateThumbprint(TestingSignerThumbprint, nameof(TestingSignerThumbprint));
        ValidateDistinctThumbprints(production, testing);
    }

    public string? GetConfiguredThumbprint(string channel) => channel switch
    {
        "Production" => Normalize(ProductionSignerThumbprint),
        "Testing" => Normalize(TestingSignerThumbprint),
        _ => null
    };

    private static string ValidateThumbprint(string value, string name)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            throw new ArgumentException("A signer thumbprint must be exactly 40 hexadecimal characters.", name);
        }

        return normalized;
    }

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray()).ToUpperInvariant();
        return normalized.Length == 40 && normalized.All(Uri.IsHexDigit) ? normalized : null;
    }

    public static void ValidateDistinctThumbprints(string? production, string? testing)
    {
        if (production is not null
            && testing is not null
            && string.Equals(production, testing, StringComparison.Ordinal))
        {
            throw new ArgumentException("Production and Testing signer thumbprints must be cryptographically distinct.");
        }
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
