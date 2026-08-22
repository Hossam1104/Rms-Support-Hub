using System.Reflection;
using RmsSupportHub.Pos.Agent.Rms;
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
}
