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

    [Fact]
    public void EnsureProvisioned_SecuresEveryCreatedDirectoryInTheChain()
    {
        var nestedPath = Path.Combine(_rootDirectory, "nested", "leaf");

        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(nestedPath);

        foreach (var directory in new[] { _rootDirectory, Path.Combine(_rootDirectory, "nested"), nestedPath })
        {
            var security = new DirectoryInfo(directory).GetAccessControl();
            Assert.True(security.AreAccessRulesProtected);

            var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>();
            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            Assert.DoesNotContain(rules, rule => rule.IdentityReference == everyone && rule.AccessControlType == AccessControlType.Allow);
        }
    }

    [Fact]
    public void EnsureProvisioned_FailsClosedWhenTheTargetIsAFile()
    {
        File.WriteAllText(_rootDirectory, "not-a-directory");

        Assert.Throws<UnauthorizedAccessException>(() => ServiceOwnedDirectoryProvisioner.EnsureProvisioned(_rootDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
        else if (File.Exists(_rootDirectory))
        {
            File.Delete(_rootDirectory);
        }
    }
}
