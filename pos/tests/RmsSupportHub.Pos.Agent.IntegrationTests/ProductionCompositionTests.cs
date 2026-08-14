using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Routing;
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

    [Fact]
    public void ProductionMapsInt08ServiceControlWithoutLaterFeatureRoutes()
    {
        using var factory = new AgentWebApplicationFactory("Production");
        using var scope = factory.Services.CreateScope();
        var endpoints = scope.ServiceProvider
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Where(path => path is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("/api/v1/device/identity", endpoints);
        Assert.Contains("/api/v1/device/connectivity", endpoints);
        Assert.Contains("/api/v1/device/capabilities", endpoints);
        Assert.Contains("/api/v1/configuration", endpoints);
        Assert.Contains("/api/v1/services", endpoints);
        Assert.Contains("/api/v1/services/{serviceId}/actions", endpoints);
        Assert.Contains("/api/v1/rms/diagnostics", endpoints);
        Assert.DoesNotContain(endpoints, path => path!.Contains("backup", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(endpoints, path => path!.Contains("restore", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(endpoints, path => path!.Contains("maintenance", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(endpoints, path => path!.Contains("downloader", StringComparison.OrdinalIgnoreCase));
    }
}
