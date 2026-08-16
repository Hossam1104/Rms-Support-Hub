using System.Globalization;

namespace RmsSupportHub.Pos.Application.Packages;

/// <summary>Bounded, invariant three-part package version ordering.</summary>
public static class AgentPackageVersion
{
    public static bool TryParse(string? value, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('.', StringSplitOptions.None);
        if (parts.Length != 3 || parts.Any(part => part.Length is 0 or > 9 || part.Any(character => character is < '0' or > '9')))
        {
            return false;
        }

        if (parts.Any(part => part.Length > 1 && part[0] == '0')) return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch)
            || major < 0 || minor < 0 || patch < 0)
        {
            return false;
        }

        version = new Version(major, minor, patch);
        return true;
    }

    public static int Compare(string left, string right)
    {
        if (!TryParse(left, out var leftVersion) || !TryParse(right, out var rightVersion))
        {
            throw new ArgumentException("Package versions must use bounded major.minor.patch form.");
        }

        return leftVersion!.CompareTo(rightVersion);
    }
}
