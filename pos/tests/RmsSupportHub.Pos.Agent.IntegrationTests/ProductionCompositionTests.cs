using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.Extensions.DependencyInjection;
using RmsSupportHub.Pos.Agent.Authorization;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class ProductionCompositionTests
{
    [Fact]
    public async Task ProductionRegistersNegotiateAndNeverTheTestScheme()
    {
        using var factory = new AgentWebApplicationFactory("Production");
        using var scope = factory.Services.CreateScope();
        var schemes = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.NotNull(await schemes.GetSchemeAsync(NegotiateDefaults.AuthenticationScheme));
        Assert.Null(await schemes.GetSchemeAsync(TestSupport.FakeAuthenticationHandler.SchemeName));
        Assert.IsType<WindowsAdministratorGroupChecker>(
            scope.ServiceProvider.GetRequiredService<IAdministratorGroupChecker>());
    }

    [Fact]
    public async Task IntegrationTestUsesFakeSchemeOnlyInTheDedicatedEnvironment()
    {
        using var factory = new AgentWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var schemes = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.NotNull(await schemes.GetSchemeAsync(TestSupport.FakeAuthenticationHandler.SchemeName));
        Assert.Null(await schemes.GetSchemeAsync(NegotiateDefaults.AuthenticationScheme));
    }
}
