using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RmsSupportHub.Api.Configuration;
using Xunit;

namespace RmsSupportHub.Tests;

public sealed class SupportHubOptionsTests
{
    [Fact]
    public void ValidatorRejectsUnknownDeploymentTier()
    {
        var options = ValidTestingOptions();
        options.DeploymentTier = "Preview";

        var result = Validate(options, Configuration());

        Assert.True(result.Failed);
        Assert.Contains("DeploymentTier", result.FailureMessage);
    }

    [Fact]
    public void ValidatorRejectsEnabledEnvironmentWithMissingServerMappings()
    {
        var options = ValidTestingOptions();
        options.Environments["upc_ecommerce"]["UPC Testing"].ApiEndpointKey = null;
        options.Environments["upc_ecommerce"]["UPC Testing"].ConnectionStringName = null;

        var result = Validate(options, new ConfigurationBuilder().Build());

        Assert.True(result.Failed);
        Assert.Contains("ApiEndpointKey", result.FailureMessage);
        Assert.Contains("ConnectionStringName", result.FailureMessage);
    }

    [Fact]
    public void ValidatorAllowsDisabledOptionalEnvironmentWithoutSecrets()
    {
        var options = new SupportHubOptions
        {
            Environments = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ghc_ecommerce"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["GHC Testing"] = new() { Enabled = false }
                },
                ["ghc_unicommerce"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["GHC Uni-Commerce Testing"] = new() { Enabled = false }
                }
            }
        };

        var result = Validate(options, new ConfigurationBuilder().Build());

        Assert.False(result.Failed, result.FailureMessage);
    }

    [Fact]
    public void CatalogMarksMissingDatabaseSecretUnavailableWithoutFailingStartup()
    {
        var options = ValidTestingOptions();
        var catalog = ConfiguredEnvironmentCatalog.Build(Configuration(), options);

        var testing = catalog["upc_ecommerce"]["UPC Testing"];

        Assert.False(testing.Available);
        Assert.False(testing.HealthProbeEnabled);
    }

    [Fact]
    public void ValidTestingRegistrationPassesStructuralValidation()
    {
        var options = ValidTestingOptions();

        var result = Validate(options, Configuration(includeSecret: true));

        Assert.False(result.Failed, result.FailureMessage);
    }

    private static SupportHubOptions ValidTestingOptions() => new()
    {
        DeploymentTier = "Testing",
        Environments = new(StringComparer.OrdinalIgnoreCase)
        {
            ["upc_ecommerce"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["UPC Testing"] = new()
                {
                    Enabled = true,
                    ApiEndpointKey = "UpcTesting",
                    CancelEndpointKey = "UpcTesting",
                    ConnectionStringName = "UpcEcommerceTest"
                }
            }
        }
    };

    private static IConfiguration Configuration(bool includeSecret = false)
    {
        var values = new Dictionary<string, string?>
        {
            ["ModuleEndpoints:UpcTesting"] = "https://testing.example/orders",
            ["ModuleCancelEndpoints:UpcTesting"] = "https://testing.example/cancel"
        };
        if (includeSecret)
            values["ConnectionStrings:UpcEcommerceTest"] = "Server=127.0.0.1;Database=RmsSupportHubTest;Integrated Security=True;";

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(
        SupportHubOptions options,
        IConfiguration configuration) =>
        new SupportHubOptionsValidator(configuration).Validate(Options.DefaultName, options);
}
