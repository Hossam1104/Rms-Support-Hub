using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Installation;

namespace RmsSupportHub.Pos.Infrastructure.Diagnostics;

/// <summary>
/// Resolves only exact manifest executables beneath the fixed RMS installation roots. It does not
/// accept browser paths, arguments, working directories, environment variables, or shell text.
/// </summary>
public sealed class DiagnosticConsolePolicy(
    IDiagnosticConsoleManifest manifest,
    RmsInstallationOptions installationOptions) : IDiagnosticConsolePolicy
{
    public bool TryCreateLaunchSpec(
        DiagnosticConsoleTarget target,
        RmsInstallationSnapshot installation,
        out DiagnosticConsoleExecutableDefinition definition,
        out DiagnosticConsoleLaunchSpec? launchSpec,
        out string? failureCode)
    {
        launchSpec = null;
        failureCode = null;
        if (!manifest.TryGet(target, out definition!))
        {
            failureCode = "console_target_not_allowlisted";
            return false;
        }

        var root = target switch
        {
            DiagnosticConsoleTarget.BranchServerApi => installationOptions.BranchServerDirectory,
            DiagnosticConsoleTarget.CashierServerApi => installationOptions.CashierServerDirectory,
            DiagnosticConsoleTarget.ServiceManager => installationOptions.ServicesManagerDirectory,
            DiagnosticConsoleTarget.CashierUi => installationOptions.CashierUiDirectory,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(root) || !IsSafeRoot(root))
        {
            failureCode = "console_root_unavailable";
            return false;
        }

        var fullRoot = Path.GetFullPath(root);
        var executablePath = Path.Combine(fullRoot, definition.ExecutableFileName);
        if (IsReparsePoint(fullRoot)
            || !IsChildOf(executablePath, fullRoot)
            || !File.Exists(executablePath)
            || IsReparsePoint(executablePath)
            || !installation.InstallationDetected)
        {
            failureCode = "console_executable_unavailable";
            return false;
        }

        if (definition.ArgumentTemplate.Any(argument => argument.Any(char.IsControl) || argument.Length > 128))
        {
            failureCode = "console_manifest_invalid";
            return false;
        }

        launchSpec = new(
            executablePath,
            definition.ArgumentTemplate.ToArray(),
            fullRoot,
            new Dictionary<string, string>(StringComparer.Ordinal));
        return true;
    }

    private static bool IsSafeRoot(string path)
    {
        try
        {
            return Path.IsPathFullyQualified(path)
                && path.Length <= 260
                && !path.Any(char.IsControl)
                && !path.Contains("..", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsChildOf(string child, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var normalizedChild = Path.GetFullPath(child);
        if (string.Equals(normalizedRoot, normalizedChild, StringComparison.OrdinalIgnoreCase)) return false;
        var relative = Path.GetRelativePath(normalizedRoot, normalizedChild);
        return !Path.IsPathFullyQualified(relative)
            && !relative.Equals("..", StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static bool IsReparsePoint(string path)
    {
        try { return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint); }
        catch { return true; }
    }
}
