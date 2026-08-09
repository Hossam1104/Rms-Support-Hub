using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using RmsSupportHub.Api.Controllers;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Repositories;
using RmsSupportHub.Core.Services;
using RmsSupportHub.Core.DTOs;
using Xunit;

namespace RmsSupportHub.Tests;

public class UpcProductionRoutingTests
{
    private sealed class CapturingItemRepository : IItemRepository
    {
        public string? LastConnectionString { get; private set; }

        public Task<Product?> LookupItemAsync(string connectionString, string materialNumber, string? branchCode = null)
        {
            LastConnectionString = connectionString;
            return Task.FromResult<Product?>(null);
        }
    }

    private sealed class CapturingConsumerRepository : IConsumerRepository
    {
        public string? LastConnectionString { get; private set; }

        public Task<Consumer?> LookupConsumerByPhoneAsync(string connectionString, string phone)
        {
            LastConnectionString = connectionString;
            return Task.FromResult<Consumer?>(null);
        }
    }

    private sealed class SingleModuleRegistry : IModuleRegistry
    {
        private readonly IOrderModule _module;

        public SingleModuleRegistry(IOrderModule module) => _module = module;

        public IReadOnlyCollection<IOrderModule> GetAllModules() => new[] { _module };
        public IOrderModule? GetModule(string key) => key == _module.Key ? _module : null;
        public IOrderModule GetModuleOrThrow(string key) => GetModule(key) ?? throw new KeyNotFoundException(key);
    }

    private sealed class EmptyBranchRepository : IBranchRepository
    {
        public Task<List<BranchOptionDto>> ListBranchesAsync(string connectionString) =>
            Task.FromResult(new List<BranchOptionDto>());
    }

    private static IConfiguration BuildConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:UpcEcommerceTest"] =
                "Data Source=upc-sql.example;Initial Catalog=RmsMainTest2;Integrated Security=True;Encrypt=False"
        })
        .Build();

    private static SqlConnectionSnapshot ReadConnection(string connectionString)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        return new SqlConnectionSnapshot(builder.DataSource, builder.InitialCatalog);
    }

    private sealed record SqlConnectionSnapshot(string Server, string Database);

    [Fact]
    public async Task ItemAndConsumerReadsFollowTheSelectedUpcEnvironment()
    {
        var itemRepository = new CapturingItemRepository();
        var consumerRepository = new CapturingConsumerRepository();
        var module = new UpcEcommerceModule(
            new FlatOrderPayloadBuilder(),
            new FlatOrderValidator(),
            itemRepository,
            consumerRepository);
        var controller = new LookupController(
            new SingleModuleRegistry(module),
            BuildConfiguration(),
            new EmptyBranchRepository(),
            new MemoryCache(new MemoryCacheOptions()));

        await controller.LookupItem("upc_ecommerce", "123456", "100", "UPC Production");
        await controller.LookupConsumer("upc_ecommerce", "0556028080", "UPC Production");

        Assert.NotNull(itemRepository.LastConnectionString);
        Assert.NotNull(consumerRepository.LastConnectionString);
        Assert.Equal("upc-sql.example", ReadConnection(itemRepository.LastConnectionString!).Server);
        Assert.Equal("RmsMainProd", ReadConnection(itemRepository.LastConnectionString!).Database);
        Assert.Equal("RmsMainProd", ReadConnection(consumerRepository.LastConnectionString!).Database);

        await controller.LookupItem("upc_ecommerce", "123456", "100", "UPC Testing");
        await controller.LookupConsumer("upc_ecommerce", "0556028080", "UPC Testing");

        Assert.Equal("RmsMainTest2", ReadConnection(itemRepository.LastConnectionString!).Database);
        Assert.Equal("RmsMainTest2", ReadConnection(consumerRepository.LastConnectionString!).Database);
    }
}
