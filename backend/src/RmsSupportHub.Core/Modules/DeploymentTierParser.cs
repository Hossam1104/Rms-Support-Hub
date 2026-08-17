namespace RmsSupportHub.Core.Modules;

/// <summary>The single strict-textual-allowlist authority for
/// <c>SupportHub:DeploymentTier</c>. Deliberately does not use
/// <see cref="Enum.TryParse{TEnum}(string?,bool,out TEnum)"/>, which accepts
/// the enum's underlying numeric representation (e.g. "1" resolves to
/// <see cref="DeploymentTier.Production"/>) -- an unsafe failure direction for
/// malformed server configuration. Both <c>SupportHubOptionsValidator</c> and
/// the composition root must call this same method so the accepted/runtime
/// interpretation of a configured value can never diverge.</summary>
public static class DeploymentTierParser
{
    public static bool TryParseExact(string? value, out DeploymentTier tier)
    {
        if (string.Equals(value, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            tier = DeploymentTier.Testing;
            return true;
        }

        if (string.Equals(value, "Production", StringComparison.OrdinalIgnoreCase))
        {
            tier = DeploymentTier.Production;
            return true;
        }

        tier = default;
        return false;
    }
}
