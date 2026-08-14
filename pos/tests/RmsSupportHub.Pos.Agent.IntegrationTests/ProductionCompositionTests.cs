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
    public void ProductionMapsTypedRmsDatabaseRoutesAlongsideServiceControl()
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
        Assert.Contains("/api/v1/rms/databases/{targetId}", endpoints);
        Assert.Contains("/api/v1/rms/databases/{targetId}/backup", endpoints);
        Assert.Contains("/api/v1/rms/databases/{targetId}/restore", endpoints);
        Assert.Contains("/api/v1/rms/databases/{targetId}/operations/{operationId}", endpoints);
        Assert.Contains("/api/v1/rms/databases/{targetId}/operations/{operationId}/events", endpoints);
        Assert.Contains("/api/v1/downloads/branches", endpoints);
        Assert.Contains("/api/v1/downloads/batches", endpoints);
        Assert.Contains("/api/v1/maintenance/cleanup/preview", endpoints);
        Assert.Contains("/api/v1/maintenance/cleanup/execute", endpoints);
        Assert.Contains("/api/v1/maintenance/reset/preview", endpoints);
        Assert.Contains("/api/v1/maintenance/reset/execute", endpoints);
        Assert.Contains("/api/v1/artifacts/{artifactId}", endpoints);
    }
}
