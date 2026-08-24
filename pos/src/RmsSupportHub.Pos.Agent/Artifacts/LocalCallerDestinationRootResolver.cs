using Microsoft.Win32;

namespace RmsSupportHub.Pos.Agent.Artifacts;

public sealed record LocalCallerDestinationRoot(string Category, string FullPath);

public interface ILocalCallerProfilePathProvider
{
    string? TryGetProfilePath(string principalSid);
}

/// <summary>
/// Reads the machine-owned ProfileList mapping. The SID is supplied only by the authenticated
/// Named Pipe identity; no client profile or root string is accepted.
/// </summary>
public sealed class WindowsProfileListPathProvider : ILocalCallerProfilePathProvider
{
    private const string ProfileListPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";

    public string? TryGetProfilePath(string principalSid)
    {
        if (!IsSafeSid(principalSid))
        {
            return null;
        }

        try
        {
            using var profileList = Registry.LocalMachine.OpenSubKey(ProfileListPath, writable: false);
            using var profile = profileList?.OpenSubKey(principalSid, writable: false);
            return profile?.GetValue("ProfileImagePath", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        }
        catch
        {
            return null;
        }
    }

    internal static bool IsSafeSid(string? value) =>
        value is { Length: > 0 and <= 184 }
        && value.StartsWith("S-", StringComparison.OrdinalIgnoreCase)
        && value.All(character => char.IsLetterOrDigit(character) || character == '-');
}

/// <summary>
/// Resolves the three fixed, caller-owned output roots. All validation happens in the Agent from
/// the SID-to-profile mapping, never from LocalSystem's interactive-folder environment.
/// </summary>
public sealed class WindowsProfileListDestinationRootResolver(
    ILocalCallerProfilePathProvider profilePathProvider) : ILocalCallerDestinationRootResolver
{
    private const int MaximumPathLength = 260;

    public bool TryResolve(
        string principalSid,
        out IReadOnlyList<LocalCallerDestinationRoot> roots)
    {
        roots = [];
        if (!WindowsProfileListPathProvider.IsSafeSid(principalSid))
        {
            return false;
        }

        var profilePath = profilePathProvider.TryGetProfilePath(principalSid);
        if (!TryCanonicalizeProfilePath(profilePath, out var profileRoot)
            || !Directory.Exists(profileRoot)
            || HasReparsePoint(profileRoot))
        {
            return false;
        }

        var candidates = new[]
        {
            new LocalCallerDestinationRoot("desktop", Path.Combine(profileRoot, "Desktop")),
            new LocalCallerDestinationRoot("documents", Path.Combine(profileRoot, "Documents")),
            new LocalCallerDestinationRoot("downloads", Path.Combine(profileRoot, "Downloads"))
        };

        var validated = new List<LocalCallerDestinationRoot>(candidates.Length);
        foreach (var candidate in candidates)
        {
            if (!TryCanonicalizeLocalPath(candidate.FullPath, out var root)
                || !IsWithinProfile(profileRoot, root)
                || !Directory.Exists(root)
                || HasReparsePoint(root))
            {
                return false;
            }

            validated.Add(candidate with { FullPath = root });
        }

        roots = validated;
        return true;
    }

    private static bool TryCanonicalizeProfilePath(string? path, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || path.Any(char.IsControl))
        {
            return false;
        }

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path);
            if (!TryCanonicalizeLocalPath(expanded, out canonical))
            {
                return false;
            }

            var leaf = Path.GetFileName(canonical);
            return !string.IsNullOrWhiteSpace(leaf) && leaf is not "." and not "..";
        }
        catch
        {
            canonical = string.Empty;
            return false;
        }
    }

    private static bool TryCanonicalizeLocalPath(string? path, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(path)
            || path.Length > MaximumPathLength
            || path.Any(char.IsControl)
            || path.StartsWith("\\\\", StringComparison.Ordinal)
            || path.StartsWith("//", StringComparison.Ordinal)
            || path.StartsWith(@"\\?\", StringComparison.Ordinal)
            || path.StartsWith(@"\\.\", StringComparison.Ordinal)
            || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        try
        {
            canonical = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var driveRoot = Path.GetPathRoot(canonical);
            return driveRoot is { Length: 3 }
                && char.IsLetter(driveRoot[0])
                && driveRoot[1] == ':'
                && driveRoot[2] == Path.DirectorySeparatorChar
                && canonical.Length <= MaximumPathLength;
        }
        catch
        {
            canonical = string.Empty;
            return false;
        }
    }

    private static bool IsWithinProfile(string profileRoot, string path) =>
        string.Equals(profileRoot, path, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(profileRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool HasReparsePoint(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            return true;
        }
    }
}

public interface ILocalCallerDestinationRootResolver
{
    bool TryResolve(string principalSid, out IReadOnlyList<LocalCallerDestinationRoot> roots);
}
