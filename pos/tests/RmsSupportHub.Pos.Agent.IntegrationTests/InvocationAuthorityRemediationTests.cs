using System.Reflection;
using System.Security.Principal;
using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Invocation;
using RmsSupportHub.Pos.Agent.Rms;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Application.Invocation;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class InvocationAuthorityRemediationTests
{
    [Fact]
    public void DiagnosticsHasNoPublicOverloadThatCanManufactureAuthority()
    {
        var overloads = typeof(RmsDiagnosticsService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == "GetAsync")
            .ToArray();

        Assert.NotEmpty(overloads);
        Assert.All(overloads, method => Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(InvocationContext)));
    }

    [Theory]
    [InlineData(InvocationAuthorizationLevel.LocalAdministrator)]
    [InlineData(InvocationAuthorizationLevel.LocalOperator)]
    [InlineData(InvocationAuthorizationLevel.Unauthenticated)]
    public void RealLocalWpfFactoryPreservesTrustedSidSourceAndCorrelation(
        InvocationAuthorizationLevel authority)
    {
        var callerSid = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-2301");
        var factory = CreateFactory(new FixedAuthorityClassifier(callerSid, authority, succeeds: true));
        using var identity = WindowsIdentity.GetCurrent();

        var context = factory.CreateLocalWpf(
            identity,
            new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-2302"),
            "trusted-correlation");

        Assert.Equal(InvocationSource.LocalWpf, context.Source);
        Assert.Equal(callerSid.Value, context.AuthenticatedCaller);
        Assert.Equal(authority, context.AuthorizationLevel);
        Assert.Equal("trusted-correlation", context.CorrelationId);
    }

    [Fact]
    public void RealLocalWpfFactoryClassifiesSystemAsLocalAdministrator()
    {
        var factory = CreateFactory(new FixedAuthorityClassifier(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            InvocationAuthorizationLevel.LocalAdministrator,
            succeeds: true));

        using var identity = WindowsIdentity.GetCurrent();
        var context = factory.CreateLocalWpf(identity, CreateOperatorSid(), "system-correlation");

        Assert.Equal(InvocationAuthorizationLevel.LocalAdministrator, context.AuthorizationLevel);
    }

    [Fact]
    public void RealLocalWpfFactoryFailsClosedWhenIdentitySidIsUnavailable()
    {
        var factory = CreateFactory(new FixedAuthorityClassifier(null, InvocationAuthorizationLevel.Unauthenticated, succeeds: false));
        using var identity = WindowsIdentity.GetCurrent();

        var context = factory.CreateLocalWpf(identity, CreateOperatorSid(), "missing-sid");

        Assert.Equal(InvocationAuthorizationLevel.Unauthenticated, context.AuthorizationLevel);
        Assert.Equal(string.Empty, context.AuthenticatedCaller);
    }

    [Fact]
    public void RealLocalWpfFactoryFailsClosedWhenRoleLookupThrows()
    {
        var factory = CreateFactory(new ThrowingAuthorityClassifier());
        using var identity = WindowsIdentity.GetCurrent();

        var context = factory.CreateLocalWpf(identity, CreateOperatorSid(), "role-failure");

        Assert.Equal(InvocationAuthorizationLevel.Unauthenticated, context.AuthorizationLevel);
        Assert.Equal(string.Empty, context.AuthenticatedCaller);
    }

    private static AgentInvocationContextFactory CreateFactory(ILocalWindowsAuthorityClassifier classifier) =>
        new(new NoAdministratorGroupChecker(), new NoPrincipalSidResolver(), classifier);

    private static SecurityIdentifier CreateOperatorSid() =>
        new("S-1-5-21-1111111111-2222222222-3333333333-2303");

    private sealed class FixedAuthorityClassifier(
        SecurityIdentifier? callerSid,
        InvocationAuthorizationLevel authority,
        bool succeeds) : ILocalWindowsAuthorityClassifier
    {
        public bool TryClassify(
            WindowsIdentity identity,
            SecurityIdentifier operatorGroupSid,
            out SecurityIdentifier? resolvedSid,
            out InvocationAuthorizationLevel authorizationLevel)
        {
            resolvedSid = callerSid;
            authorizationLevel = authority;
            return succeeds;
        }
    }

    private sealed class ThrowingAuthorityClassifier : ILocalWindowsAuthorityClassifier
    {
        public bool TryClassify(
            WindowsIdentity identity,
            SecurityIdentifier operatorGroupSid,
            out SecurityIdentifier? callerSid,
            out InvocationAuthorizationLevel authorizationLevel) =>
            throw new InvalidOperationException("simulated role lookup failure");
    }

    private sealed class NoAdministratorGroupChecker : IAdministratorGroupChecker
    {
        public bool IsInAdministratorsGroup(System.Security.Claims.ClaimsPrincipal principal) => false;
    }

    private sealed class NoPrincipalSidResolver : IAgentPrincipalSidResolver
    {
        public bool TryGetSid(System.Security.Claims.ClaimsPrincipal principal, out string sid)
        {
            sid = string.Empty;
            return false;
        }
    }
}
