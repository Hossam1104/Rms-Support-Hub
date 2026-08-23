namespace RmsSupportHub.Pos.Agent.Artifacts;

/// <summary>
/// Validates only a user-selected local output destination. It never resolves or enumerates an
/// Agent-owned source path, and it rejects network, traversal, alternate-data-stream, protected,
/// and reparse-point escapes.
/// </summary>
public sealed class LocalArtifactDestinationPolicy
{
    private readonly IReadOnlyList<string> allowedRoots;

    public LocalArtifactDestinationPolicy()
        : this(DiscoverAllowedRoots())
    {
    }

    public LocalArtifactDestinationPolicy(IEnumerable<string> allowedRoots)
    {
        this.allowedRoots = (allowedRoots ?? [])
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(Canonicalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool TryValidate(
        string? destinationPath,
        string expectedExtension,
        out LocalArtifactDestination destination)
    {
        destination = default;
        if (string.IsNullOrWhiteSpace(destinationPath)
            || destinationPath.Length > 512
            || destinationPath.Any(char.IsControl)
            || !Path.IsPathRooted(destinationPath)
            || destinationPath.StartsWith("\\\\", StringComparison.Ordinal)
            || destinationPath.StartsWith("//", StringComparison.Ordinal)
            || destinationPath.Contains("\\\\", StringComparison.Ordinal)
            || destinationPath.Contains("//", StringComparison.Ordinal)
            || destinationPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or ".."))
        {
            return false;
        }

        string fullPath;
        string parent;
        string fileName;
        try
        {
            fullPath = Canonicalize(destinationPath);
            parent = Path.GetDirectoryName(fullPath) ?? string.Empty;
            fileName = Path.GetFileName(fullPath);
        }
        catch
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(parent)
            || string.IsNullOrWhiteSpace(fileName)
            || fileName.Any(char.IsControl)
            || fileName.Contains(':')
            || fileName is "." or ".."
            || !string.Equals(Path.GetExtension(fileName), expectedExtension, StringComparison.OrdinalIgnoreCase)
            || IsReservedDeviceName(fileName))
        {
            return false;
        }

        var invalid = Path.GetInvalidFileNameChars();
        if (fileName.Any(invalid.Contains))
        {
            return false;
        }

        var matchedRoot = allowedRoots.FirstOrDefault(root => IsWithinRoot(root, parent));
        if (matchedRoot is null
            || !Directory.Exists(parent)
            || HasReparsePointInPath(matchedRoot, parent)
            || (File.Exists(fullPath) && HasReparsePoint(fullPath))
            || Directory.Exists(fullPath))
        {
            return false;
        }

        var category = CategoryFor(matchedRoot, parent);
        var temporaryPath = Path.Combine(parent, $".{fileName}.{Guid.NewGuid():N}.tmp");
        if (temporaryPath.Any(char.IsControl) || !IsWithinRoot(matchedRoot, temporaryPath))
        {
            return false;
        }

        destination = new(fullPath, parent, fileName, category, temporaryPath);
        return true;
    }

    private static IReadOnlyList<string> DiscoverAllowedRoots()
    {
        var roots = new List<string>();
        foreach (var folder in new[]
                 {
                     Environment.SpecialFolder.DesktopDirectory,
                     Environment.SpecialFolder.MyDocuments,
                     Environment.SpecialFolder.UserProfile
                 })
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(path))
            {
                roots.Add(folder == Environment.SpecialFolder.UserProfile
                    ? Path.Combine(path, "Downloads")
                    : path);
            }
        }

        return roots;
    }

    private static string Canonicalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool IsWithinRoot(string root, string target) =>
        string.Equals(root, target, StringComparison.OrdinalIgnoreCase)
        || target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool HasReparsePointInPath(string root, string path)
    {
        var current = path;
        while (!string.IsNullOrWhiteSpace(current) && IsWithinRoot(root, current))
        {
            if (HasReparsePoint(current)) return true;
            var parent = Directory.GetParent(current)?.FullName;
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent ?? string.Empty;
        }

        return false;
    }

    private static bool HasReparsePoint(string path)
    {
        try
        {
            return (File.Exists(path) || Directory.Exists(path))
                && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            return true;
        }
    }

    private static string CategoryFor(string root, string parent)
    {
        var leaf = Path.GetFileName(root);
        return leaf.Equals("Desktop", StringComparison.OrdinalIgnoreCase)
            ? "desktop"
            : leaf.Equals("Downloads", StringComparison.OrdinalIgnoreCase)
                ? "downloads"
                : leaf.Equals("Documents", StringComparison.OrdinalIgnoreCase)
                    ? "documents"
                    : "approved-local";
    }

    private static bool IsReservedDeviceName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName).TrimEnd('.', ' ');
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (stem.Length == 4
                && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && char.IsDigit(stem[3]));
    }
}

public readonly record struct LocalArtifactDestination(
    string FullPath,
    string ParentDirectory,
    string FileName,
    string Category,
    string TemporaryPath);
