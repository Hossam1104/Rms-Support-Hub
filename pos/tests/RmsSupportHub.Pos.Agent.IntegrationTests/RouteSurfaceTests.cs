using System.Net;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class RouteSurfaceTests : IClassFixture<AgentWebApplicationFactory>
{
    private readonly AgentWebApplicationFactory _factory;

    public RouteSurfaceTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void CurrentInt08RouteCategoriesAreMapped()
    {
        using var scope = _factory.Services.CreateScope();
        var endpoints = scope.ServiceProvider
            .GetServices<Microsoft.AspNetCore.Routing.EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Where(path => path is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("/health/live", endpoints);
        Assert.Contains("/health/ready", endpoints);
        Assert.Contains("/api/v1/session", endpoints);
        Assert.Contains("/api/v1/security/mutation-token", endpoints);
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
        Assert.DoesNotContain(endpoints, path => path!.Contains("maintenance", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(endpoints, path => path!.Contains("downloader", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(endpoints, path => path!.Equals("/api/v1/artifacts", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/api/v1/backup/options")]
    [InlineData("/api/v1/downloader/branches")]
    [InlineData("/api/v1/restore/options")]
    [InlineData("/api/v1/maintenance/options")]
    [InlineData("/api/v1/operations")]
    [InlineData("/api/v1/artifacts")]
    [InlineData("/api/v1/security/other")]
    public async Task LaterFeatureRoutesRemainUnmapped(string path)
    {
        using var client = _factory.CreateAdminClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
