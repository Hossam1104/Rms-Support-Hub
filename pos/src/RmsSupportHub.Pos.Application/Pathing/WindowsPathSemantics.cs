namespace RmsSupportHub.Pos.Application.Pathing;

/// <summary>
/// Deterministic syntax helpers for paths owned by the Windows POS Agent.
///
/// The Application project is intentionally portable, but maintenance roots and SMB entries are
/// Windows contracts.  These helpers keep their parsing and containment rules independent of the
/// host operating system running a test or a portable build.
/// </summary>
internal static class WindowsPathSemantics
{
    private const char Separator = '\\';

    public static bool IsUnc(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Normalize(path).StartsWith(@"\\", StringComparison.Ordinal);

    public static bool IsDriveRelative(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = Normalize(path);
        return normalized.Length >= 2
            && char.IsLetter(normalized[0])
            && normalized[1] == ':'
            && (normalized.Length == 2 || normalized[2] != Separator);
    }

    public static bool IsFullyQualified(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = Normalize(path.Trim());
        return IsDriveAbsolute(normalized) || IsUnc(normalized);
    }

    public static bool TryCanonicalize(
        string? rawPath,
        bool allowUnc,
        out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(rawPath)) return false;

        var normalized = Normalize(rawPath.Trim());
        if (normalized.Any(character => char.IsControl(character))) return false;
        if (normalized.StartsWith(@"\\?\", StringComparison.Ordinal)
            || normalized.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return false;
        }

        if (IsDriveRelative(normalized)) return false;

        string root;
        string remainder;
        if (IsUnc(normalized))
        {
            if (!allowUnc || !TryGetUncRoot(normalized, out root, out remainder)) return false;
        }
        else
        {
            if (!IsDriveAbsolute(normalized)) return false;
            root = normalized[..3];
            remainder = normalized[3..];
        }

        var components = new List<string>();
        foreach (var component in remainder.Split(Separator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (component == ".") continue;
            if (component == "..")
            {
                if (components.Count == 0) return false;
                components.RemoveAt(components.Count - 1);
                continue;
            }

            if (!IsSafeComponent(component)) return false;
            components.Add(component);
        }

        canonical = components.Count == 0
            ? root
            : root + string.Join(Separator, components);
        return true;
    }

    public static bool IsRoot(string canonicalPath)
    {
        if (string.IsNullOrWhiteSpace(canonicalPath)) return false;
        var normalized = Normalize(canonicalPath);
        if (IsDriveAbsolute(normalized)) return normalized.Length == 3;
        return IsUnc(normalized)
            && TryGetUncRoot(normalized, out var root, out var remainder)
            && string.IsNullOrEmpty(remainder)
            && StringComparer.OrdinalIgnoreCase.Equals(
                TrimTrailingSeparators(normalized),
                TrimTrailingSeparators(root));
    }

    public static bool IsWithinOrEqual(string candidate, string root)
    {
        var normalizedCandidate = TrimTrailingSeparators(candidate);
        var normalizedRoot = TrimTrailingSeparators(root);
        return StringComparer.OrdinalIgnoreCase.Equals(normalizedCandidate, normalizedRoot)
            || normalizedCandidate.StartsWith(normalizedRoot + Separator, StringComparison.OrdinalIgnoreCase);
    }

    public static bool OverlapsByContainment(string candidate, string boundary) =>
        IsWithinOrEqual(candidate, boundary)
        || IsWithinOrEqual(boundary, candidate);

    public static string TrimTrailingSeparators(string path)
    {
        var normalized = Normalize(path);
        if (IsDriveAbsolute(normalized) && normalized.Length == 3) return normalized[..2];
        return normalized.TrimEnd(Separator);
    }

    public static string? GetFileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var normalized = Normalize(path.Trim());
        if (normalized.EndsWith(Separator)) return null;

        var separatorIndex = normalized.LastIndexOf(Separator);
        var fileName = separatorIndex >= 0 ? normalized[(separatorIndex + 1)..] : normalized;
        return fileName.Length == 0 ? null : fileName;
    }

    public static string? GetDirectoryName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var normalized = Normalize(path.Trim());
        if (normalized.EndsWith(Separator)) return null;

        var separatorIndex = normalized.LastIndexOf(Separator);
        return separatorIndex < 0 ? null : normalized[..separatorIndex];
    }

    private static bool IsDriveAbsolute(string path) =>
        path.Length >= 3
        && char.IsLetter(path[0])
        && path[1] == ':'
        && path[2] == Separator;

    private static bool TryGetUncRoot(string path, out string root, out string remainder)
    {
        root = string.Empty;
        remainder = string.Empty;
        if (!IsUnc(path)) return false;

        var parts = path[2..].Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2
            || !IsSafeComponent(parts[0])
            || !IsSafeComponent(parts[1]))
        {
            return false;
        }

        root = $@"\\{parts[0]}\{parts[1]}\";
        remainder = parts.Length == 2
            ? string.Empty
            : string.Join(Separator, parts.Skip(2));
        return true;
    }

    private static bool IsSafeComponent(string component) =>
        component.Length > 0
        && component is not "." and not ".."
        && !component.Any(character => character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*')
        && !component.Any(char.IsControl)
        && component[^1] is not ('.' or ' ');

    private static string Normalize(string path) => path.Replace('/', Separator);
}
