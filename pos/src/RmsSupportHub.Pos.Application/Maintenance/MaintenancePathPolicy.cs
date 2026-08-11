using System.Text.RegularExpressions;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Maintenance;

/// <summary>
/// Canonical managed-path policy for destructive maintenance.  The policy deliberately resolves
/// and validates every path at the point of use; a browser never supplies a path or a root ID.
/// </summary>
public sealed class MaintenancePathPolicy
{
    private readonly IMaintenanceFileSystem _fileSystem;

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly Regex UnresolvedEnvironmentVariableRegex = new(
        @"%[^%\r\n]+%",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SafeIdentifierRegex = new(
        @"^[A-Za-z0-9_.-]{1,128}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public MaintenancePathPolicy(IMaintenanceFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public MaintenancePathResolutionResult Resolve(
        string targetId,
        string rawPath,
        MaintenanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var safeTargetId = SafeLogicalValue(targetId, "target");
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.InvalidPath);
        }

        if (IsDriveRelative(rawPath))
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.DriveRelativePath);
        }

        string expanded;
        try
        {
            expanded = _fileSystem.ExpandEnvironmentVariables(rawPath.Trim());
        }
        catch
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.InvalidPath);
        }

        if (UnresolvedEnvironmentVariableRegex.IsMatch(expanded))
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.UnresolvedEnvironmentVariable);
        }

        if (IsDriveRelative(expanded))
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.DriveRelativePath);
        }

        if (IsUnc(expanded) && !settings.AllowUncPaths)
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.UncNotAllowed);
        }

        if (!Path.IsPathFullyQualified(expanded))
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.InvalidPath);
        }

        string canonicalTarget;
        try
        {
            canonicalTarget = _fileSystem.GetFullPath(expanded);
        }
        catch
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.InvalidPath);
        }

        if (IsRootLevel(canonicalTarget))
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.RootTarget);
        }

        if (!TryCanonicalizeRequiredRoots(settings.ManagedRoots, settings.AllowUncPaths, out var managedRoots))
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.NoManagedRoots);
        }

        if (!TryCanonicalizeRequiredRoots(settings.ProtectedRoots, settings.AllowUncPaths, out var protectedRoots))
        {
            return Reject(safeTargetId, settings.ProtectedRoots is null or { Count: 0 }
                ? MaintenanceFailureCodes.NoProtectedRoots
                : MaintenanceFailureCodes.InvalidConfiguration);
        }

        if (!TryCanonicalizeRequiredRoots(settings.InstallRoots, settings.AllowUncPaths, out var installRoots))
        {
            return Reject(safeTargetId, settings.InstallRoots is null or { Count: 0 }
                ? MaintenanceFailureCodes.NoInstallRoots
                : MaintenanceFailureCodes.InvalidConfiguration);
        }

        if (!TryCanonicalizeRequiredRoots(settings.DataRoots, settings.AllowUncPaths, out var dataRoots))
        {
            return Reject(safeTargetId, settings.DataRoots is null or { Count: 0 }
                ? MaintenanceFailureCodes.NoDataRoots
                : MaintenanceFailureCodes.InvalidConfiguration);
        }

        var managedRoot = managedRoots.FirstOrDefault(root => IsWithinOrEqual(canonicalTarget, root));

        // A managed root itself is never a cleanup target.  This also keeps a root-level target
        // from becoming a recursive delete of the entire configured data area.
        if (managedRoot is not null
            && PathComparer.Equals(TrimSeparator(canonicalTarget), TrimSeparator(managedRoot)))
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.RootTarget);
        }

        if (protectedRoots.Any(root => OverlapsByContainment(canonicalTarget, root)))
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.ProtectedRoot);
        }

        if (installRoots.Any(root => OverlapsByContainment(canonicalTarget, root)))
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.InstallRoot);
        }

        if (managedRoot is null)
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.OutsideManagedRoot);
        }

        if (!dataRoots.Any(root => IsWithinOrEqual(canonicalTarget, root)))
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.NotDataRoot);
        }

        var inspections = _fileSystem.InspectAncestors(canonicalTarget);
        if (inspections.Any(item => item.InspectionFailed))
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.ReparseInspectionFailed);
        }

        foreach (var inspection in inspections.Where(item => item.IsReparsePoint))
        {
            if (settings.RejectReparsePoints)
            {
                return Reject(safeTargetId, MaintenanceFailureCodes.ReparsePoint);
            }

            if (string.IsNullOrWhiteSpace(inspection.ResolvedLinkTarget))
            {
                return Reject(safeTargetId, MaintenanceFailureCodes.ReparseEscape);
            }

            if (!TryCanonicalize(inspection.ResolvedLinkTarget, settings.AllowUncPaths, out var linkTarget)
                || !IsWithinOrEqual(linkTarget, managedRoot)
                || !dataRoots.Any(root => IsWithinOrEqual(linkTarget, root))
                || protectedRoots.Any(root => OverlapsByContainment(linkTarget, root))
                || installRoots.Any(root => OverlapsByContainment(linkTarget, root)))
            {
                return Reject(safeTargetId, MaintenanceFailureCodes.ReparseEscape);
            }
        }

        var targetInspection = _fileSystem.Inspect(canonicalTarget);
        if (targetInspection.InspectionFailed)
        {
            return Reject(safeTargetId, MaintenanceFailureCodes.PathInspectionFailed);
        }

        if (targetInspection.IsReparsePoint)
        {
            if (settings.RejectReparsePoints)
            {
                return Reject(safeTargetId, MaintenanceFailureCodes.ReparsePoint);
            }

            if (string.IsNullOrWhiteSpace(targetInspection.ResolvedLinkTarget)
                || !TryCanonicalize(targetInspection.ResolvedLinkTarget, settings.AllowUncPaths, out var targetLink)
                || !IsWithinOrEqual(targetLink, managedRoot)
                || !dataRoots.Any(root => IsWithinOrEqual(targetLink, root))
                || protectedRoots.Any(root => OverlapsByContainment(targetLink, root))
                || installRoots.Any(root => OverlapsByContainment(targetLink, root)))
            {
                return Reject(safeTargetId, MaintenanceFailureCodes.ReparseEscape);
            }
        }

        return new MaintenancePathResolutionResult(
            true,
            null,
            null,
            new MaintenancePathResolution(
                safeTargetId,
                canonicalTarget,
                managedRoot,
                targetInspection.Exists,
                targetInspection.IsDirectory,
                targetInspection.LengthBytes,
                targetInspection.ChildCount));
    }

    public static string SafeLogicalValue(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var trimmed = value.Trim();
        return SafeIdentifierRegex.IsMatch(trimmed) ? trimmed : fallback;
    }

    public static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && SafeIdentifierRegex.IsMatch(value.Trim());

    private bool TryCanonicalizeRoots(
        IEnumerable<string>? roots,
        bool allowUnc,
        out List<string> canonicalRoots)
    {
        canonicalRoots = [];
        var valid = true;
        foreach (var rawRoot in roots ?? [])
        {
            if (TryCanonicalize(rawRoot, allowUnc, out var canonical)
                && !IsRootLevel(canonical))
            {
                if (!canonicalRoots.Any(existing => PathComparer.Equals(existing, canonical)))
                {
                    canonicalRoots.Add(canonical);
                }
            }
            else
            {
                valid = false;
            }
        }

        return valid;
    }

    private bool TryCanonicalizeRequiredRoots(
        IEnumerable<string>? roots,
        bool allowUnc,
        out List<string> canonicalRoots)
    {
        return TryCanonicalizeRoots(roots, allowUnc, out canonicalRoots)
            && canonicalRoots.Count > 0;
    }

    private bool TryCanonicalize(string? rawPath, bool allowUnc, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(rawPath)) return false;
        if (IsDriveRelative(rawPath)) return false;

        string expanded;
        try
        {
            expanded = _fileSystem.ExpandEnvironmentVariables(rawPath.Trim());
            if (UnresolvedEnvironmentVariableRegex.IsMatch(expanded)
                || (IsUnc(expanded) && !allowUnc)
                || !Path.IsPathFullyQualified(expanded))
            {
                return false;
            }

            canonical = _fileSystem.GetFullPath(expanded);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWithinOrEqual(string candidate, string root)
    {
        var normalizedCandidate = TrimSeparator(candidate);
        var normalizedRoot = TrimSeparator(root);
        return PathComparer.Equals(normalizedCandidate, normalizedRoot)
            || normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, PathComparison)
            || normalizedCandidate.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, PathComparison);
    }

    private static bool OverlapsByContainment(string candidate, string boundary) =>
        IsWithinOrEqual(candidate, boundary)
        || IsWithinOrEqual(boundary, candidate);

    private static string TrimSeparator(string path) =>
        path.Length > 3 ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : path;

    private static bool IsRootLevel(string path)
    {
        var root = Path.GetPathRoot(path);
        return !string.IsNullOrWhiteSpace(root)
            && PathComparer.Equals(TrimSeparator(path), TrimSeparator(root));
    }

    private static bool IsUnc(string path) => path.StartsWith(@"\\", StringComparison.Ordinal);

    private static bool IsDriveRelative(string path) =>
        path.Length >= 2
        && char.IsLetter(path[0])
        && path[1] == ':'
        && (path.Length == 2 || path[2] is not ('\\' or '/'));

    private static MaintenancePathResolutionResult Reject(string targetId, string code) =>
        new(false, code, MaintenanceFailureCodes.PathRejectedMessage, null);
}

public sealed record MaintenancePathResolutionResult(
    bool Accepted,
    string? RejectionCode,
    string? SafeMessage,
    MaintenancePathResolution? Resolution);
