using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.MainServer;

public sealed class MainServerProfileCatalog : IMainServerProfileCatalog
{
    private readonly MainServerProfileOptions options;
    private readonly IReadOnlyList<MainServerProfileDefinition> profiles;

    public MainServerProfileCatalog(MainServerProfileOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        profiles =
        [
            new(
                this.options.TestingProfileId,
                MainServerEnvironment.Testing,
                new Uri(this.options.TestingBaseAddress, UriKind.Absolute),
                this.options.ClientName,
                true,
                [MainServerReadOperation.BranchStatus, MainServerReadOperation.InstalledBranch, MainServerReadOperation.InstalledPos]),
            new(
                this.options.ProductionProfileId,
                MainServerEnvironment.Production,
                new Uri(this.options.ProductionBaseAddress, UriKind.Absolute),
                this.options.ClientName,
                false,
                [MainServerReadOperation.BranchStatus, MainServerReadOperation.InstalledBranch, MainServerReadOperation.InstalledPos])
        ];
    }

    public IReadOnlyList<MainServerProfileDefinition> GetProfiles() => profiles;

    public bool TryGetActive(
        RmsInstallationSnapshot installation,
        out MainServerProfileDefinition profile,
        out MainServerBinding binding)
    {
        ArgumentNullException.ThrowIfNull(installation);
        profile = profiles[0];

        if (string.IsNullOrWhiteSpace(installation.BranchCode)
            || string.IsNullOrWhiteSpace(installation.PosNumber)
            || string.IsNullOrWhiteSpace(installation.ClientName))
        {
            binding = new(MainServerBindingState.Unavailable, installation.BranchCode, installation.PosNumber, installation.ClientName,
                "Branch, POS, and client identity are required before Main Server reads are attempted.");
            return false;
        }

        if (!installation.ClientName.StartsWith(options.ClientName, StringComparison.OrdinalIgnoreCase))
        {
            binding = new(MainServerBindingState.Mismatch, installation.BranchCode, installation.PosNumber, installation.ClientName,
                "The discovered client is not bound to the server-owned profile.");
            return false;
        }

        if (!IsSameEndpoint(installation.MainServerUrl, profile.BaseAddress))
        {
            binding = new(MainServerBindingState.Mismatch, installation.BranchCode, installation.PosNumber, installation.ClientName,
                "The discovered Main Server endpoint does not match the active server-owned profile.");
            return false;
        }

        binding = new(MainServerBindingState.Bound, installation.BranchCode, installation.PosNumber, installation.ClientName,
            "The active Testing profile is bound to the discovered Branch/POS identity and Main Server endpoint.");
        return profile.Enabled;
    }

    private static bool IsSameEndpoint(string? discovered, Uri profile)
    {
        if (string.IsNullOrWhiteSpace(discovered)) return false;
        if (!Uri.TryCreate(discovered.Trim(), UriKind.Absolute, out var uri)
            || uri.UserInfo.Length > 0
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0)
        {
            return false;
        }

        var profileScheme = profile.Scheme.ToLowerInvariant();
        var candidateScheme = uri.Scheme.ToLowerInvariant();
        var profilePort = EffectivePort(profile);
        var discoveredPort = EffectivePort(uri);
        var profilePath = NormalizeBasePath(profile.AbsolutePath);
        var discoveredPath = NormalizeBasePath(uri.AbsolutePath);
        return string.Equals(candidateScheme, profileScheme, StringComparison.Ordinal)
            && string.Equals(uri.Host, profile.Host, StringComparison.OrdinalIgnoreCase)
            && discoveredPort == profilePort
            && string.Equals(discoveredPath, profilePath, StringComparison.OrdinalIgnoreCase);
    }

    private static int EffectivePort(Uri uri) => uri.IsDefaultPort
        ? string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80
        : uri.Port;

    private static string NormalizeBasePath(string path) =>
        "/" + path.Trim('/');
}
