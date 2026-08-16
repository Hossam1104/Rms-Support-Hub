using System.Security.AccessControl;
using System.Security.Principal;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Configuration;

/// <summary>
/// Creates the service-owned configuration directory chain and restricts every directory created by
/// this application to BUILTIN\Administrators and LocalSystem. Existing directories are accepted
/// only when their owner is already trusted, or when a disposable test fixture is owned by the
/// current test identity; hostile owners, reparse points, and ACLs that cannot be replaced fail
/// closed.
/// </summary>
public static class ServiceOwnedDirectoryProvisioner
{
    public static void EnsureProvisioned(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A service-owned directory path is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        if (HasParentTraversal(path) || path.Any(char.IsControl))
        {
            throw new UnauthorizedAccessException("The service-owned storage path is not canonical.");
        }

        if (!OperatingSystem.IsWindows())
        {
            RejectReparsePointsOnExistingChain(fullPath);
            if (File.Exists(fullPath)) throw new UnauthorizedAccessException("The service-owned storage path is occupied by a file.");
            Directory.CreateDirectory(fullPath);
            RejectReparsePointsOnExistingChain(fullPath);
            return;
        }

        if (IsMachineOwnedPath(fullPath) && !IsTrustedRuntimeIdentity())
        {
            throw new UnauthorizedAccessException("Machine-owned service storage requires LocalSystem or an administrator token.");
        }

        RejectReparsePointsOnExistingChain(fullPath);

        var missingDirectories = new List<string>();
        var current = fullPath;
        while (!Directory.Exists(current))
        {
            if (File.Exists(current))
            {
                throw new UnauthorizedAccessException("The service-owned storage path is occupied by a file.");
            }

            missingDirectories.Add(current);
            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("The service-owned storage path has no trusted parent.");
            }

            current = parent;
        }

        // If the machine-owned chain already exists above a new leaf, verify that existing
        // service-owned ancestor before creating anything beneath it. This also rejects an
        // existing reparse point or hostile ACL above the new protected directory.
        if (IsMachineOwnedPath(fullPath)
            && missingDirectories.Count > 0
            && IsMachineOwnedPath(current))
        {
            VerifyTrustedExistingDirectory(current);
        }

        // Secure each newly-created segment before the next segment or any file is created below it.
        foreach (var directory in missingDirectories.AsEnumerable().Reverse())
        {
            Directory.CreateDirectory(directory);
            ApplyRestrictedAcl(directory, IsTemporaryTestPath(directory));
        }

        // A caller may supply a pre-existing leaf (including a test fixture). Reconcile it only
        // after checking ownership and reparse state; ApplyRestrictedAcl replaces, rather than
        // merges with, its inherited access rules.
        if (missingDirectories.Count == 0)
        {
            ApplyRestrictedAcl(fullPath, IsTemporaryTestPath(fullPath));
        }
    }

    /// <summary>
    /// Read-only trust check for a fixed machine-owned control file. The file itself and every
    /// directory from it to the fixed service-owned root are verified before its contents become
    /// authority. The only non-machine boundary is the explicitly named disposable test fixture
    /// root; it can never make a normal %ProgramData% control file acceptable.
    /// </summary>
    public static bool IsTrustedControlFile(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !Path.IsPathFullyQualified(filePath) || filePath.Any(char.IsControl) || HasParentTraversal(filePath)) return false;
            if (!OperatingSystem.IsWindows()) return false;

            var fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath)
                || !IsSecurityControlFileName(fullPath)
                || File.GetAttributes(fullPath).HasFlag(FileAttributes.ReparsePoint)
                || new FileInfo(fullPath).Length is <= 0 or > 64 * 1024)
            {
                return false;
            }

            if (!TryResolveSecurityRoot(fullPath, out var securityRoot, out var allowTemporaryTestIdentity)) return false;

            var currentSid = allowTemporaryTestIdentity ? WindowsIdentity.GetCurrent().User : null;
            var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)) return false;

            while (true)
            {
                if (!Directory.Exists(directory)
                    || !IsRestrictedDirectory(directory, administrators, system, currentSid, allowTemporaryTestIdentity)) return false;
                if (string.Equals(directory, securityRoot, StringComparison.OrdinalIgnoreCase)) break;

                var parent = Directory.GetParent(directory)?.FullName;
                if (parent is null || string.Equals(parent, directory, StringComparison.OrdinalIgnoreCase)) return false;
                directory = parent;
            }

            return IsRestrictedFile(fullPath, administrators, system, currentSid, allowTemporaryTestIdentity);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Applies the exact file ACL used for an application-owned lifecycle checkpoint. External trust
    /// and certificate files remain deployment-owned and are never repaired by this method.
    /// </summary>
    public static void EnsureControlFileAcl(string filePath)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (string.IsNullOrWhiteSpace(filePath) || !Path.IsPathFullyQualified(filePath)) throw new ArgumentException("A fixed control file path is required.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath) || !IsSecurityControlFileName(fullPath) || !TryResolveSecurityRoot(fullPath, out _, out var allowTemporaryTestIdentity))
        {
            throw new UnauthorizedAccessException("The lifecycle control file is outside the fixed service-owned boundary.");
        }

        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var currentSid = allowTemporaryTestIdentity ? WindowsIdentity.GetCurrent().User : null;
        var fileInfo = new FileInfo(fullPath);
        var existing = fileInfo.GetAccessControl(AccessControlSections.Owner);
        var existingOwner = existing.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
        if (existingOwner is null
            || (!IsTrustedIdentity(existingOwner, administrators, system)
                && !(allowTemporaryTestIdentity && existingOwner == currentSid)))
        {
            throw new UnauthorizedAccessException("The existing control file owner is not trusted.");
        }

        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        if (!allowTemporaryTestIdentity) security.SetOwner(administrators);
        security.AddAccessRule(new FileSystemAccessRule(administrators, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, AccessControlType.Allow));
        if (allowTemporaryTestIdentity && currentSid is not null && !IsTrustedIdentity(currentSid, administrators, system))
        {
            security.AddAccessRule(new FileSystemAccessRule(currentSid, FileSystemRights.FullControl, AccessControlType.Allow));
        }

        fileInfo.SetAccessControl(security);
        if (!IsTrustedControlFile(fullPath)) throw new UnauthorizedAccessException("The control file ACL could not be verified.");
    }

    private static bool IsRestrictedDirectory(
        string directoryPath,
        SecurityIdentifier administrators,
        SecurityIdentifier system,
        SecurityIdentifier? currentSid,
        bool allowTemporaryTestIdentity)
    {
        try
        {
            var directoryInfo = new DirectoryInfo(directoryPath);
            if (directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;

            var security = directoryInfo.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
            var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
            var ownerTrusted = IsTrustedIdentity(owner, administrators, system)
                || (allowTemporaryTestIdentity && owner is not null && owner == currentSid);
            if (!ownerTrusted || !security.AreAccessRulesProtected) return false;

            var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>();
            if (rules.Any(rule => rule.AccessControlType == AccessControlType.Allow
                && !IsTrustedIdentity(rule.IdentityReference as SecurityIdentifier, administrators, system)
                && !(allowTemporaryTestIdentity && rule.IdentityReference == currentSid))) return false;

            return allowTemporaryTestIdentity
                ? rules.Any(rule => rule.IdentityReference == currentSid
                    && rule.AccessControlType == AccessControlType.Allow
                    && rule.FileSystemRights.HasFlag(FileSystemRights.FullControl))
                : rules.Count(rule => rule.IdentityReference == administrators
                    && rule.AccessControlType == AccessControlType.Allow
                    && rule.FileSystemRights.HasFlag(FileSystemRights.FullControl)) > 0
                    && rules.Count(rule => rule.IdentityReference == system
                        && rule.AccessControlType == AccessControlType.Allow
                        && rule.FileSystemRights.HasFlag(FileSystemRights.FullControl)) > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRestrictedFile(
        string filePath,
        SecurityIdentifier administrators,
        SecurityIdentifier system,
        SecurityIdentifier? currentSid,
        bool allowTemporaryTestIdentity)
    {
        var security = new FileInfo(filePath).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
        var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
        var ownerTrusted = IsTrustedIdentity(owner, administrators, system)
            || (allowTemporaryTestIdentity && owner is not null && owner == currentSid);
        if (!ownerTrusted || !security.AreAccessRulesProtected) return false;

        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToList();
        if (rules.Any(rule => rule.AccessControlType == AccessControlType.Allow
            && !IsTrustedIdentity(rule.IdentityReference as SecurityIdentifier, administrators, system)
            && !(allowTemporaryTestIdentity && rule.IdentityReference == currentSid))) return false;

        return allowTemporaryTestIdentity
            ? rules.Any(rule => rule.IdentityReference == currentSid
                && rule.AccessControlType == AccessControlType.Allow
                && rule.FileSystemRights.HasFlag(FileSystemRights.FullControl))
            : rules.Any(rule => rule.IdentityReference == administrators
                && rule.AccessControlType == AccessControlType.Allow
                && rule.FileSystemRights.HasFlag(FileSystemRights.FullControl))
                && rules.Any(rule => rule.IdentityReference == system
                    && rule.AccessControlType == AccessControlType.Allow
                    && rule.FileSystemRights.HasFlag(FileSystemRights.FullControl));
    }

    private static bool IsSecurityControlFileName(string path) =>
        Path.GetFileName(path) is "package-trust.json" or "agent-certificate.json" or "lifecycle-state.json";

    private static bool TryResolveSecurityRoot(
        string fullPath,
        out string securityRoot,
        out bool allowTemporaryTestIdentity)
    {
        var machineRoot = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "DBS",
            AgentProductIdentity.ProductId));
        var testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "RmsSupportHub.Pos.Tests"));

        if (IsWithinRoot(fullPath, machineRoot))
        {
            securityRoot = machineRoot;
            allowTemporaryTestIdentity = false;
            return true;
        }

        if (IsWithinRoot(fullPath, testRoot))
        {
            securityRoot = testRoot;
            allowTemporaryTestIdentity = true;
            return true;
        }

        securityRoot = string.Empty;
        allowTemporaryTestIdentity = false;
        return false;
    }

    private static bool IsWithinRoot(string path, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var normalizedPath = Path.GetFullPath(path);
        return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyRestrictedAcl(string path, bool allowTemporaryTestIdentity)
    {
        var directoryInfo = new DirectoryInfo(path);
        if (directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new UnauthorizedAccessException("Reparse-point service-owned storage is not accepted.");
        }

        var existingSecurity = directoryInfo.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
        var existingOwner = existingSecurity.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var currentIdentity = WindowsIdentity.GetCurrent();
        var currentSid = currentIdentity.User;

        if (existingOwner is null
            || (!IsTrustedIdentity(existingOwner, administrators, system)
                && !(allowTemporaryTestIdentity && existingOwner == currentSid)))
        {
            throw new UnauthorizedAccessException("The existing service-owned directory owner is not trusted.");
        }

        var security = new DirectorySecurity();

        // Disable inheritance and discard any inherited rules so the ACL is exactly what we set below.
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        if (!allowTemporaryTestIdentity)
        {
            security.SetOwner(administrators);
        }
        security.AddAccessRule(new FileSystemAccessRule(
            administrators,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            system,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        if (allowTemporaryTestIdentity && currentSid is not null && !IsTrustedIdentity(currentSid, administrators, system))
        {
            security.AddAccessRule(new FileSystemAccessRule(
                currentSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        directoryInfo.SetAccessControl(security);
        VerifyRestrictedAcl(directoryInfo, administrators, system, allowTemporaryTestIdentity, currentSid);
    }

    private static void VerifyRestrictedAcl(
        DirectoryInfo directoryInfo,
        SecurityIdentifier administrators,
        SecurityIdentifier system,
        bool allowTemporaryTestIdentity,
        SecurityIdentifier? currentSid)
    {
        var security = directoryInfo.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
        var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
        if ((!IsTrustedIdentity(owner, administrators, system)
                && !(allowTemporaryTestIdentity && owner == currentSid))
            || !security.AreAccessRulesProtected)
        {
            throw new UnauthorizedAccessException("The service-owned directory ACL could not be verified.");
        }

        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToList();

        if (rules.Any(rule => rule.AccessControlType == AccessControlType.Allow
            && !IsTrustedIdentity(rule.IdentityReference as SecurityIdentifier, administrators, system)
            && !(allowTemporaryTestIdentity && rule.IdentityReference == currentSid)))
        {
            throw new UnauthorizedAccessException("The service-owned directory ACL contains an untrusted allow rule.");
        }

        if (!rules.Any(rule => rule.IdentityReference == administrators
            && rule.AccessControlType == AccessControlType.Allow
            && rule.FileSystemRights.HasFlag(FileSystemRights.FullControl)))
        {
            throw new UnauthorizedAccessException("The service-owned directory ACL has no administrator control rule.");
        }
    }

    private static bool IsTrustedIdentity(
        SecurityIdentifier? identity,
        SecurityIdentifier administrators,
        SecurityIdentifier system) =>
        identity is not null
        && (identity == administrators || identity == system);

    private static bool IsTrustedRuntimeIdentity()
    {
        var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User;
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        return sid is not null
            && (sid == system || new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator));
    }

    private static bool IsMachineOwnedPath(string fullPath)
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var machineParent = Path.GetFullPath(Path.Combine(programData, "DBS"));
        return fullPath.Equals(machineParent, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(machineParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemporaryTestPath(string fullPath)
    {
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.StartsWith(temporaryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void RejectReparsePointsOnExistingChain(string fullPath)
    {
        var current = fullPath;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(current) || File.Exists(current))
            {
                try
                {
                    if (File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                    {
                        throw new UnauthorizedAccessException("Reparse-point service-owned storage is not accepted.");
                    }
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }

            var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(current));
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent;
        }
    }

    private static void VerifyTrustedExistingDirectory(string path)
    {
        var directory = new DirectoryInfo(path);
        if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new UnauthorizedAccessException("Reparse-point service-owned storage is not accepted.");
        }

        var security = directory.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
        var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        if (!IsTrustedIdentity(owner, administrators, system) || !security.AreAccessRulesProtected)
        {
            throw new UnauthorizedAccessException("The existing service-owned parent is not trusted.");
        }

        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>();
        if (rules.Any(rule => rule.AccessControlType == AccessControlType.Allow
            && !IsTrustedIdentity(rule.IdentityReference as SecurityIdentifier, administrators, system)))
        {
            throw new UnauthorizedAccessException("The existing service-owned parent contains an untrusted allow rule.");
        }
    }

    private static bool HasParentTraversal(string path) =>
        path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or "..");
}
