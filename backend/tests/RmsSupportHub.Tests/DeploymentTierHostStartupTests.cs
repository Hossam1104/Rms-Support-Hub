using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RmsSupportHub.Core.Modules;
using Xunit;

namespace RmsSupportHub.Tests;

/// <summary>M-1 regression: proves the composition root -- not just the
/// options validator in isolation -- fails closed for a coerced/numeric
/// DeploymentTier, and that the two intended textual tokens still produce a
/// running host with the matching authority. No fake/in-memory config
/// duplicates the validator's parsing rules; both go through
/// DeploymentTierParser.TryParseExact.</summary>
[Collection("HostEnvironmentCollection")]
public sealed class DeploymentTierHostStartupTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("2")]
    [InlineData("Staging")]
    public void NumericOrMalformedDeploymentTier_FailsHostStartup(string configuredTier)
    {
        using var factory = new DeploymentTierWebApplicationFactory(configuredTier);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("SupportHub:DeploymentTier must be Testing or Production.", CollectMessages(exception));
    }

    [Fact]
    public void TextualTesting_StartsHostWithTestingAuthority()
    {
        using var factory = new DeploymentTierWebApplicationFactory("Testing");
        using var client = factory.CreateClient();

        var policy = factory.Services.GetRequiredService<IEnvironmentPolicy>();
        Assert.Equal(DeploymentTier.Testing, policy.DeploymentTier);
    }

    [Fact]
    public void TextualProduction_StartsHostWithProductionAuthorityOnlyAsExplicitConfiguration()
    {
        using var factory = new DeploymentTierWebApplicationFactory("Production");
        using var client = factory.CreateClient();

        var policy = factory.Services.GetRequiredService<IEnvironmentPolicy>();
        Assert.Equal(DeploymentTier.Production, policy.DeploymentTier);
    }

    /// <summary>N-2 regression: remove the complete application configuration
    /// source so the value cannot be inherited from appsettings.json. The
    /// bound options object must retain its safe Testing property default.
    /// </summary>
    [Fact]
    public async Task OmittedDeploymentTier_DefaultsToTesting()
    {
        using var factory = new OmittedDeploymentTierWebApplicationFactory();
        using var client = factory.CreateClient();

        var policy = factory.Services.GetRequiredService<IEnvironmentPolicy>();
        Assert.Equal(DeploymentTier.Testing, policy.DeploymentTier);

        var response = await client.GetAsync("/api/health/ready");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"deploymentTier\":\"Testing\"", body);
    }

    private static string CollectMessages(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
            messages.Add(current.Message);
        return string.Join(" | ", messages);
    }

    private sealed class DeploymentTierWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _configuredTier;

        public DeploymentTierWebApplicationFactory(string configuredTier)
        {
            _configuredTier = configuredTier;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SupportHub:DeploymentTier"] = _configuredTier,
                    ["ConnectionStrings:UpcEcommerceTest"] =
                        "Server=127.0.0.1;Database=RmsSupportHubTest;Integrated Security=True;TrustServerCertificate=True;"
                }));
        }
    }

    private sealed class OmittedDeploymentTierWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SupportHub:AllowCustomEndpoints"] = "false",
                    ["SupportHub:HealthProbe:Enabled"] = "false",
                    ["SupportHub:HealthProbe:TimeoutSeconds"] = "3"
                });
            });
        }
    }
}
