using System.Security.AccessControl;
using System.Security.Principal;

namespace RmsSupportHub.Pos.Infrastructure.Configuration;

/// <summary>
/// Creates the service-owned configuration directory and restricts its ACL to Administrators and
/// the service identity (plan section 5.5). ADR-012's dedicated service account is not provisioned
/// until the Session 14 installer runs, so the identity currently executing the Agent process stands
/// in for "the service identity" until then — it is granted exactly the rights the real service
/// account will need.
/// </summary>
public static class ServiceOwnedDirectoryProvisioner
{
    public static void EnsureProvisioned(string path)
    {
        Directory.CreateDirectory(path);

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        ApplyRestrictedAcl(path);
    }

    private static void ApplyRestrictedAcl(string path)
    {
        var directoryInfo = new DirectoryInfo(path);
        var security = new DirectorySecurity();

        // Disable inheritance and discard any inherited rules so the ACL is exactly what we set below.
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        security.AddAccessRule(new FileSystemAccessRule(
            administrators,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        var serviceIdentity = WindowsIdentity.GetCurrent().User;
        if (serviceIdentity is not null && serviceIdentity != administrators)
        {
            security.AddAccessRule(new FileSystemAccessRule(
                serviceIdentity,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        directoryInfo.SetAccessControl(security);
    }
}
