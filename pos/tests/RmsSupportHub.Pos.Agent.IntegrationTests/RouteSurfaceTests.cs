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
    public async Task OnlyFoundationRouteCategoriesAreMapped()
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
        Assert.DoesNotContain(endpoints, path => path!.Contains("backup", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(endpoints, path => path!.Contains("restore", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(endpoints, path => path!.Contains("maintenance", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(endpoints, path => path!.Contains("downloader", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(endpoints, path => path!.Contains("artifact", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(endpoints, path => path!.Contains("service", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(endpoints, path => path!.Contains("configuration", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/api/v1/backup/options")]
    [InlineData("/api/v1/downloader/branches")]
    [InlineData("/api/v1/restore/options")]
    [InlineData("/api/v1/maintenance/options")]
    [InlineData("/api/v1/services")]
    [InlineData("/api/v1/configuration")]
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
