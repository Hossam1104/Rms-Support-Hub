using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using RmsSupportHub.Pos.Agent.Authorization;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class WindowsAdministratorGroupCheckerTests
{
    [Fact]
    public void LocalizedDirectAdministratorsMembershipIsRecognizedByWellKnownSid()
    {
        var lookup = new FakeLookup
        {
            GroupNames = ["Administrateurs"],
            GroupSids =
            {
                ["Administrateurs"] = BuiltinAdministratorsSid()
            }
        };

        var resolver = CreateResolver(lookup);

        Assert.True(resolver.IsMemberOfBuiltinAdministrators(WindowsIdentity.GetCurrent()));
    }

    [Fact]
    public void IndirectAdministratorsMembershipIsRecognizedWhenReturnedByWindows()
    {
        var lookup = new FakeLookup
        {
            // NetUserGetLocalGroups with LG_INCLUDE_INDIRECT returns the local group reached via
            // the user's domain/global group as well as any direct local-group memberships.
            GroupNames = ["Domain Support Administrators", "Administrateurs"],
            GroupSids =
            {
                ["Domain Support Administrators"] = new SecurityIdentifier("S-1-5-21-10-20-30-4000"),
                ["Administrateurs"] = BuiltinAdministratorsSid()
            }
        };

        var resolver = CreateResolver(lookup);

        Assert.True(resolver.IsMemberOfBuiltinAdministrators(WindowsIdentity.GetCurrent()));
    }

    [Fact]
    public void NonAdministratorMembershipReturnsFalse()
    {
        var lookup = new FakeLookup
        {
            GroupNames = ["Users"],
            GroupSids =
            {
                ["Users"] = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, domainSid: null)
            }
        };

        var resolver = CreateResolver(lookup);

        Assert.False(resolver.IsMemberOfBuiltinAdministrators(WindowsIdentity.GetCurrent()));
    }

    [Fact]
    public void AccountResolutionFailureFailsClosed()
    {
        var lookup = new FakeLookup { ResolveAccountName = false };

        Assert.False(CreateResolver(lookup).IsMemberOfBuiltinAdministrators(WindowsIdentity.GetCurrent()));
    }

    [Fact]
    public void LocalGroupQueryFailureFailsClosed()
    {
        var lookup = new FakeLookup { GetLocalGroups = false };

        Assert.False(CreateResolver(lookup).IsMemberOfBuiltinAdministrators(WindowsIdentity.GetCurrent()));
    }

    [Fact]
    public void GroupSidResolutionFailureFailsClosed()
    {
        var lookup = new FakeLookup
        {
            GroupNames = ["Unresolvable group"]
        };

        Assert.False(CreateResolver(lookup).IsMemberOfBuiltinAdministrators(WindowsIdentity.GetCurrent()));
    }

    [Fact]
    public void UnexpectedWindowsLookupFailureFailsClosed()
    {
        var lookup = new FakeLookup { ThrowOnGroupQuery = true };

        Assert.False(CreateResolver(lookup).IsMemberOfBuiltinAdministrators(WindowsIdentity.GetCurrent()));
    }

    [Fact]
    public void NativeLookupResolvesTheCurrentWindowsAccountAndLocalGroups()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var lookup = new WindowsLocalGroupMembershipLookup();

        Assert.NotNull(identity.User);
        Assert.True(lookup.TryResolveAccountName(identity.User!, out var accountName));
        Assert.True(lookup.TryGetLocalGroupNames(accountName, out var groupNames));
        Assert.NotNull(groupNames);
    }

    [Fact]
    public void CheckerRejectsNonWindowsIdentityWithoutCallingResolver()
    {
        var resolver = new RecordingResolver(true);
        var checker = new WindowsAdministratorGroupChecker(resolver);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "test")], "test"));

        Assert.False(checker.IsInAdministratorsGroup(principal));
        Assert.False(resolver.WasCalled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckerUsesTheSharedMembershipResolver(bool expected)
    {
        var resolver = new RecordingResolver(expected);
        var checker = new WindowsAdministratorGroupChecker(resolver);
        var principal = new ClaimsPrincipal(new WindowsIdentity(WindowsIdentity.GetCurrent().Token));

        Assert.Equal(expected, checker.IsInAdministratorsGroup(principal));
        Assert.True(resolver.WasCalled);
    }

    private static WindowsLocalGroupMembershipResolver CreateResolver(FakeLookup lookup) =>
        new(
            lookup,
            new HttpContextAccessor(),
            NullLogger<WindowsLocalGroupMembershipResolver>.Instance);

    private static SecurityIdentifier BuiltinAdministratorsSid() =>
        new(WellKnownSidType.BuiltinAdministratorsSid, domainSid: null);

    private sealed class FakeLookup : IWindowsLocalGroupMembershipLookup
    {
        public bool ResolveAccountName { get; init; } = true;

        public bool GetLocalGroups { get; init; } = true;

        public bool ThrowOnGroupQuery { get; init; }

        public IReadOnlyList<string> GroupNames { get; init; } = [];

        public Dictionary<string, SecurityIdentifier> GroupSids { get; init; } = new(StringComparer.Ordinal);

        public bool TryResolveAccountName(SecurityIdentifier accountSid, out string accountName)
        {
            accountName = ResolveAccountName ? "TESTDOMAIN\\test-user" : string.Empty;
            return ResolveAccountName;
        }

        public bool TryGetLocalGroupNames(string accountName, out IReadOnlyList<string> groupNames)
        {
            if (ThrowOnGroupQuery)
            {
                throw new InvalidOperationException("synthetic native lookup failure");
            }

            groupNames = GroupNames;
            return GetLocalGroups;
        }

        public bool TryResolveSid(string accountName, out SecurityIdentifier sid) =>
            GroupSids.TryGetValue(accountName, out sid!);
    }

    private sealed class RecordingResolver(bool result) : IWindowsLocalGroupMembershipResolver
    {
        public bool WasCalled { get; private set; }

        public bool IsMemberOfBuiltinAdministrators(WindowsIdentity identity)
        {
            WasCalled = true;
            return result;
        }
    }
}
