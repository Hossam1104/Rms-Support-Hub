using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Repositories;
using RmsSupportHub.Core.Services;
using RmsSupportHub.Data;
using RmsSupportHub.Data.Repositories;
using Xunit;

namespace RmsSupportHub.Tests;

public class ModuleRegistryTests
{
    private readonly ModuleRegistry _registry = new(
        new FlatOrderPayloadBuilder(),
        new FlatOrderValidator(),
        new UniCommercePayloadBuilder(),
        new UniCommerceValidator(),
        new FlatOrderItemRepository(new SqlServerConnectionFactory()),
        new GhcConsumerRepository(new SqlServerConnectionFactory()),
        new GhcUnicommerceConsumerRepository(new SqlServerConnectionFactory()),
        new UpcItemRepository(new SqlServerConnectionFactory()),
        new UpcConsumerRepository(new SqlServerConnectionFactory()));

    [Fact]
    public void GetAllModules_ReturnsAllFiveModules()
    {
        var modules = _registry.GetAllModules();
        Assert.Equal(5, modules.Count);
    }

    [Theory]
    [InlineData("ghc_ecommerce", "GHC E-Commerce", true)]
    [InlineData("upc_ecommerce", "UPC E-Commerce", true)]
    [InlineData("ghc_unicommerce", "GHC Uni-Commerce", true)]
    [InlineData("oms", "OMS (Order Management)", false)]
    [InlineData("call_center", "Call Center Ordering", false)]
    public void GetModule_ReturnsCorrectModule(string key, string expectedLabel, bool expectedAvailable)
    {
        var module = _registry.GetModule(key);
        Assert.NotNull(module);
        Assert.Equal(expectedLabel, module.Label);
        Assert.Equal(expectedAvailable, module.Available);
    }

    [Fact]
    public void UpcEcommerceModule_ResolvesProductionAndTestingEnvironments()
    {
        // Server/database/credentials are resolved at request time from
        // IConfiguration (user-secrets in dev, environment variables in prod) via
        // ConnectionStringResolver — never hardcoded on the module. This test only
        // covers environment-key resolution, which is real behaviour.
        var upc = _registry.GetModuleOrThrow("upc_ecommerce");
        var prodEnv = upc.GetEnvironment("UPC Production");
        var testEnv = upc.GetEnvironment("UPC Testing");

        Assert.Equal("UPC Production", prodEnv.Key);
        Assert.Equal("Production", prodEnv.Environment);
        Assert.Null(prodEnv.ApiUrl);
        Assert.Null(prodEnv.CancelUrl);
        Assert.Null(prodEnv.ConnectionStringName);
        Assert.Null(prodEnv.DatabaseOverride);
        Assert.False(prodEnv.Available);
        Assert.True(prodEnv.RequiresDatabase);
        Assert.Equal("UPC Testing", testEnv.Key);
        Assert.Equal("Testing", testEnv.Environment);
        Assert.Null(testEnv.ApiUrl);
        Assert.Null(testEnv.CancelUrl);
        Assert.Null(testEnv.ConnectionStringName);
        Assert.Null(testEnv.DatabaseOverride);
        Assert.False(testEnv.Available);
        Assert.True(testEnv.RequiresDatabase);
    }

    [Fact]
    public void GhcCapabilities_EnableOrderRequestsAndVerifiedUniCommerceConsumerLookup()
    {
        var ecommerce = _registry.GetModuleOrThrow("ghc_ecommerce");
        var uniCommerce = _registry.GetModuleOrThrow("ghc_unicommerce");

        Assert.True(ecommerce.Capabilities.OrderRequests);
        Assert.False(uniCommerce.Capabilities.ItemLookup);
        Assert.True(uniCommerce.Capabilities.ConsumerLookup);
        Assert.True(uniCommerce.Capabilities.OrderRequests);
        Assert.Equal(OrderRequestHistoryMode.ExternalInvoiceRequests,
            uniCommerce.Capabilities.OrderRequestHistory);
    }

    [Theory]
    [InlineData("GHC Uni-Commerce Testing")]
    [InlineData("GHC Uni-Commerce Production")]
    public void GhcUniCommerceLanesRequireApiAndDatabaseButNotCancel(string environmentKey)
    {
        var environment = _registry.GetModuleOrThrow("ghc_unicommerce")
            .GetEnvironment(environmentKey);

        Assert.True(environment.RequiresApiEndpoint);
        Assert.True(environment.RequiresDatabase);
        Assert.False(environment.RequiresCancelEndpoint);
        Assert.True(environment.RequiresApiKey);
    }

    [Fact]
    public void GhcUniCommerceTestingUsesTheExistingQaLaneLabel()
    {
        var environment = _registry.GetModuleOrThrow("ghc_unicommerce")
            .GetEnvironment("GHC Uni-Commerce Testing");

        Assert.Equal("QA lane", environment.RouteLabel);
    }

    [Fact]
    public async Task GhcUniCommerceConsumerLookupUsesTheVerifiedUniRepository()
    {
        var consumer = new Consumer { ConsumerCode = "C-1" };
        var consumerRepository = new RecordingConsumerRepository(consumer);
        var module = new GhcUnicommerceModule(
            new UniCommercePayloadBuilder(),
            new UniCommerceValidator(),
            consumerRepository);

        var lookedUpConsumer = await module.LookupConsumerByPhoneAsync("Server=fake;", "0555000000");

        Assert.Same(consumer, lookedUpConsumer);
        Assert.Equal(("Server=fake;", "0555000000"), consumerRepository.LastCall);
    }

    private sealed class RecordingConsumerRepository(Consumer result) : IGhcUnicommerceConsumerRepository
    {
        public (string, string) LastCall { get; private set; } = default;

        public Task<Consumer?> LookupConsumerByPhoneAsync(string connectionString, string phone)
        {
            LastCall = (connectionString, phone);
            return Task.FromResult<Consumer?>(result);
        }
    }
}
