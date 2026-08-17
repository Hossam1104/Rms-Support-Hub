using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RmsSupportHub.Api;
using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Services;
using RmsSupportHub.Core.Repositories;
using RmsSupportHub.Data;
using RmsSupportHub.Data.Repositories;
using Xunit;

namespace RmsSupportHub.Tests;

public class ConnectionStringResolverTests
{
    private static IConfiguration BuildConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:UpcEcommerceTest"] =
                "Data Source=upc-sql.example;Initial Catalog=RmsMainTest2;Integrated Security=True;Encrypt=False"
        })
        .Build();

    private static IOrderModule BuildUpcModule() => new UpcEcommerceModule(
        new FlatOrderPayloadBuilder(),
        new FlatOrderValidator(),
        new UpcItemRepository(new SqlServerConnectionFactory()),
        new UpcConsumerRepository(new SqlServerConnectionFactory()),
        TestEnvironmentCatalog.Upc());

    [Fact]
    public void Testing_UsesTheExistingTestingConnectionAndCatalog()
    {
        var module = BuildUpcModule();
        var testing = module.GetEnvironment("UPC Testing");

        var resolved = ConnectionStringResolver.RequireForEnvironment(BuildConfiguration(), testing);
        var builder = new SqlConnectionStringBuilder(resolved);

        Assert.Equal("upc-sql.example", builder.DataSource);
        Assert.Equal("RmsMainTest2", builder.InitialCatalog);
        Assert.True(builder.IntegratedSecurity);
        Assert.Null(testing.DatabaseOverride);
    }

    [Fact]
    public void Production_ReusesTestingConnectionDetailsAndOverridesOnlyTheCatalog()
    {
        var module = BuildUpcModule();
        var testing = module.GetEnvironment("UPC Testing");
        var production = module.GetEnvironment("UPC Production");
        var configuration = BuildConfiguration();

        var testingBuilder = new SqlConnectionStringBuilder(
            ConnectionStringResolver.RequireForEnvironment(configuration, testing));
        var productionBuilder = new SqlConnectionStringBuilder(
            ConnectionStringResolver.RequireForEnvironment(configuration, production));

        Assert.Equal(testing.ConnectionStringName, production.ConnectionStringName);
        Assert.Equal(testingBuilder.DataSource, productionBuilder.DataSource);
        Assert.Equal(testingBuilder.IntegratedSecurity, productionBuilder.IntegratedSecurity);
        Assert.Equal("RmsMainTest2", testingBuilder.InitialCatalog);
        Assert.Equal("RmsMainProd", productionBuilder.InitialCatalog);
    }
}
