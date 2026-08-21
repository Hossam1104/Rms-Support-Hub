using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RmsSupportHub.Core.Modules;
using Xunit;

namespace RmsSupportHub.Tests;

public sealed class ProductionTransportIntegrationTests
{
    [Fact]
    public async Task UntrustedForwardedProtoCannotTurnProductionHttpIntoHttps()
    {
        using var factory = new ProductionWebApplicationFactory(trustedProxy: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://supporthub.test")
        });
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        var response = await client.PostAsJsonAsync(
            "/api/modules/upc_ecommerce/production-unlock",
            new { password = "synthetic-owner-password" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("production_secure_transport_required", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitTrustedProxyCanNormalizeHttpsWithoutRequiringDirectTls()
    {
        using var factory = new ProductionWebApplicationFactory(trustedProxy: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        var response = await client.PostAsJsonAsync(
            "/api/modules/upc_ecommerce/production-unlock",
            new { password = "synthetic-owner-password" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("synthetic-owner-password", body, StringComparison.Ordinal);
        Assert.Contains("token", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductionHstsIsAddedOnlyOnHttpsRuntimeRequests()
    {
        using var factory = new ProductionWebApplicationFactory(trustedProxy: true, hostEnvironment: "Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://supporthub.test")
        });
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        Assert.Equal(DeploymentTier.Production, factory.Services.GetRequiredService<IEnvironmentPolicy>().DeploymentTier);
        Assert.Equal("Production", factory.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);

        var response = await client.GetAsync("/api/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.Contains("Strict-Transport-Security"),
            $"Headers: {string.Join(", ", response.Headers.Select(header => $"{header.Key}={string.Join("|", header.Value)}"))}");
    }

    private sealed class ProductionWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly bool _trustedProxy;
        private readonly string? _hostEnvironment;

        public ProductionWebApplicationFactory(bool trustedProxy, string? hostEnvironment = null)
        {
            _trustedProxy = trustedProxy;
            _hostEnvironment = hostEnvironment;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            if (_hostEnvironment is not null)
                builder.UseEnvironment(_hostEnvironment);

            var values = new Dictionary<string, string?>
            {
                ["SupportHub:DeploymentTier"] = "Production",
                ["SUPPORTHUB_PRODUCTION_UNLOCK_PASSWORD"] = "synthetic-owner-password"
            };
            if (_trustedProxy)
                values["SupportHub:ForwardedHeaders:KnownProxies:0"] = "127.0.0.1";

            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(values));
        }
    }
}
