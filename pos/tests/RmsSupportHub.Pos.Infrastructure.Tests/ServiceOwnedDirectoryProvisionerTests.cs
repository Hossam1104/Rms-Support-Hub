using System.Security.AccessControl;
using System.Security.Principal;
using RmsSupportHub.Pos.Infrastructure.Configuration;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

/// <summary>
/// Windows integration coverage for the ACL restriction required by plan section 5.5: the
/// service-owned configuration directory must disable inheritance and grant access only to
/// Administrators and the service identity (here, the identity running the test process, standing in
/// for the not-yet-provisioned service account per ADR-012).
/// </summary>
public sealed class ServiceOwnedDirectoryProvisionerTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), "pos-admin-acl-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnsureProvisioned_CreatesTheDirectory()
    {
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(_rootDirectory);

        Assert.True(Directory.Exists(_rootDirectory));
    }

    [Fact]
    public void EnsureProvisioned_DisablesAclInheritance()
    {
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(_rootDirectory);

        var security = new DirectoryInfo(_rootDirectory).GetAccessControl();

        Assert.True(security.AreAccessRulesProtected);
    }

    [Fact]
    public void EnsureProvisioned_GrantsAdministratorsFullControl()
    {
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(_rootDirectory);

        var security = new DirectoryInfo(_rootDirectory).GetAccessControl();
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>();

        Assert.Contains(rules, rule =>
            rule.IdentityReference == administrators
            && rule.AccessControlType == AccessControlType.Allow
            && rule.FileSystemRights.HasFlag(FileSystemRights.FullControl));
    }

    [Fact]
    public void EnsureProvisioned_DoesNotGrantAccessToEveryoneOrAuthenticatedUsers()
    {
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(_rootDirectory);

        var security = new DirectoryInfo(_rootDirectory).GetAccessControl();
        var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        var authenticatedUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);

        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>();

        Assert.DoesNotContain(rules, rule => rule.IdentityReference == everyone || rule.IdentityReference == authenticatedUsers);
    }

    [Fact]
    public void EnsureProvisioned_CalledTwice_IsIdempotentAndLeavesTheSameRestrictedAcl()
    {
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(_rootDirectory);
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(_rootDirectory);

        var security = new DirectoryInfo(_rootDirectory).GetAccessControl();

        Assert.True(security.AreAccessRulesProtected);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
