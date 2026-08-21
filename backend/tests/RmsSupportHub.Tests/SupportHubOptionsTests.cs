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

    /// <summary>M-1: DeploymentTier is an enum (Testing=0, Production=1), so
    /// the pre-remediation validator (Enum.TryParse) accepted numeric strings
    /// and "1" resolved to Production. Malformed/coerced server configuration
    /// must fail startup, never silently resolve to Production.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("2")]
    [InlineData("01")]
    [InlineData("+1")]
    [InlineData("Staging")]
    [InlineData("Testing;Production")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" Testing")]
    [InlineData("Testing ")]
    public void ValidatorRejectsNumericAndMalformedDeploymentTier(string invalidTier)
    {
        var options = ValidTestingOptions();
        options.DeploymentTier = invalidTier;

        var result = Validate(options, Configuration());

        Assert.True(result.Failed);
        Assert.Contains("SupportHub:DeploymentTier must be Testing or Production.", result.FailureMessage);
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("testing")]
    [InlineData("TESTING")]
    [InlineData("Production")]
    [InlineData("production")]
    [InlineData("PRODUCTION")]
    public void ValidatorAcceptsIntendedTextualDeploymentTier(string validTier)
    {
        var options = ValidTestingOptions();
        options.DeploymentTier = validTier;

        var result = Validate(options, Configuration());

        Assert.False(result.Failed, result.FailureMessage);
    }

    [Fact]
    public void ValidatorRejectsWildcardOrMalformedTrustedProxyConfiguration()
    {
        var options = ValidTestingOptions();
        options.ForwardedHeaders.KnownProxies.Add("*");
        options.ForwardedHeaders.KnownNetworks.Add("0.0.0.0/0");

        var result = Validate(options, Configuration());

        Assert.True(result.Failed);
        Assert.Contains("KnownProxies", result.FailureMessage);
        Assert.Contains("KnownNetworks", result.FailureMessage);
    }

    [Fact]
    public void ValidatorAcceptsExplicitTrustedProxyAndNetworkAllowlist()
    {
        var options = ValidTestingOptions();
        options.ForwardedHeaders.KnownProxies.Add("10.10.10.10");
        options.ForwardedHeaders.KnownNetworks.Add("10.10.20.0/24");

        var result = Validate(options, Configuration(includeSecret: true));

        Assert.False(result.Failed, result.FailureMessage);
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
    public void ValidatorRequiresAKeyReferenceForEnabledUniCommerce()
    {
        var options = new SupportHubOptions
        {
            Environments = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ghc_unicommerce"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["GHC Uni-Commerce Testing"] = new()
                    {
                        Enabled = true,
                        ApiEndpointKey = "UniTesting",
                        ConnectionStringName = "GhcUnicommerceTest"
                    }
                }
            }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ModuleEndpoints:UniTesting"] = "https://testing.example/invoice",
                ["ConnectionStrings:GhcUnicommerceTest"] = "Server=127.0.0.1;Database=RmsEcommerceStg;"
            })
            .Build();

        var result = Validate(options, configuration);

        Assert.True(result.Failed);
        Assert.Contains("ApiKeyConfigurationKey", result.FailureMessage);
    }

    [Fact]
    public void CatalogAndResolverKeepUniApiKeyServerOwned()
    {
        var options = new SupportHubOptions
        {
            Environments = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ghc_unicommerce"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["GHC Uni-Commerce Testing"] = new()
                    {
                        Enabled = true,
                        ApiEndpointKey = "UniTesting",
                        ApiKeyConfigurationKey = "UniTestingApiKey",
                        ConnectionStringName = "GhcUnicommerceTest"
                    }
                }
            }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ModuleEndpoints:UniTesting"] = "https://testing.example/invoice",
                ["ConnectionStrings:GhcUnicommerceTest"] = "Server=127.0.0.1;Database=RmsEcommerceStg;",
                ["ModuleApiKeys:UniTestingApiKey"] = "TEST-ONLY-API-KEY"
            })
            .Build();

        var environment = ConfiguredEnvironmentCatalog.Build(configuration, options)
            ["ghc_unicommerce"]["GHC Uni-Commerce Testing"];
        var resolver = new ServerOutboundApiKeyResolver(configuration);

        Assert.True(environment.Available);
        Assert.True(environment.RequiresApiKey);
        Assert.Equal("UniTestingApiKey", environment.ApiKeyConfigurationKey);
        Assert.Equal("TEST-ONLY-API-KEY", resolver.Resolve(environment));
    }

    [Fact]
    public void CatalogMarksRequiredUniApiKeyMissingAsUnavailable()
    {
        var options = new SupportHubOptions
        {
            Environments = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ghc_unicommerce"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["GHC Uni-Commerce Testing"] = new()
                    {
                        Enabled = true,
                        ApiEndpointKey = "UniTesting",
                        ApiKeyConfigurationKey = "UniTestingApiKey",
                        ConnectionStringName = "GhcUnicommerceTest"
                    }
                }
            }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ModuleEndpoints:UniTesting"] = "https://testing.example/invoice",
                ["ConnectionStrings:GhcUnicommerceTest"] = "Server=127.0.0.1;Database=RmsEcommerceStg;"
            })
            .Build();

        var environment = ConfiguredEnvironmentCatalog.Build(configuration, options)
            ["ghc_unicommerce"]["GHC Uni-Commerce Testing"];

        Assert.False(environment.Available);
        Assert.Equal("UniTestingApiKey", environment.ApiKeyConfigurationKey);
    }

    [Fact]
    public void CatalogRejectsUniApiKeyContainingHeaderLineBreak()
    {
        var options = new SupportHubOptions
        {
            Environments = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ghc_unicommerce"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["GHC Uni-Commerce Testing"] = new()
                    {
                        Enabled = true,
                        ApiEndpointKey = "UniTesting",
                        ApiKeyConfigurationKey = "UniTestingApiKey",
                        ConnectionStringName = "GhcUnicommerceTest"
                    }
                }
            }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ModuleEndpoints:UniTesting"] = "https://testing.example/invoice",
                ["ConnectionStrings:GhcUnicommerceTest"] = "Server=127.0.0.1;Database=RmsEcommerceStg;",
                ["ModuleApiKeys:UniTestingApiKey"] = "TEST-ONLY-API-KEY\r\nInjected: value"
            })
            .Build();

        var environment = ConfiguredEnvironmentCatalog.Build(configuration, options)
            ["ghc_unicommerce"]["GHC Uni-Commerce Testing"];
        var resolver = new ServerOutboundApiKeyResolver(configuration);

        Assert.False(environment.Available);
        Assert.Null(resolver.Resolve(environment));
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
