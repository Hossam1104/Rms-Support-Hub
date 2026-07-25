using OnlineOrderTool.Core.Modules;
using OnlineOrderTool.Core.Services;
using OnlineOrderTool.Data;
using OnlineOrderTool.Data.Repositories;
using Xunit;

namespace OnlineOrderTool.Tests;

public class ModuleRegistryTests
{
    private readonly ModuleRegistry _registry = new(
        new FlatOrderPayloadBuilder(),
        new FlatOrderValidator(),
        new UniCommercePayloadBuilder(),
        new UniCommerceValidator(),
        new FlatOrderItemRepository(new SqlServerConnectionFactory()),
        new GhcConsumerRepository(new SqlServerConnectionFactory()),
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
        Assert.Equal("UPC Testing", testEnv.Key);
        Assert.Equal("Testing", testEnv.Environment);
        Assert.NotEqual(prodEnv.ApiUrl, testEnv.ApiUrl);
    }
}
