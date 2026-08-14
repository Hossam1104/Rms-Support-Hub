using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Agent.Artifacts;

/// <summary>
/// Shared canonical-path safety checks for Agent-owned backup storage: every candidate path must
/// resolve strictly beneath the approved root, and no path component along the way may be a reparse
/// point (junction/symlink) that could redirect I/O outside the approved root.
/// </summary>
internal static class BackupPathSafety
{
    public static bool IsWithinRoot(string root, string target)
    {
        var canonicalRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var canonicalTarget = Path.GetFullPath(target);
        return canonicalTarget.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSafePath(IBackupFileSystem fileSystem, string root, string path)
    {
        if (!IsWithinRoot(root, path))
        {
            return false;
        }

        var current = path;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if ((fileSystem.FileExists(current) || Directory.Exists(current)) && fileSystem.IsReparsePoint(current))
            {
                return false;
            }

            var parent = Directory.GetParent(current)?.FullName;
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent ?? string.Empty;
        }

        return true;
    }
}
