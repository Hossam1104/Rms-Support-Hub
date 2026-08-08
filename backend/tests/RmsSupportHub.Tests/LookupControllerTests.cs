using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using RmsSupportHub.Api.Controllers;
using RmsSupportHub.Api.Exceptions;
using RmsSupportHub.Core.DTOs;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Repositories;
using Xunit;

namespace RmsSupportHub.Tests;

/// <summary>Proves the R6 fix for remediation_plan.md B22: a database
/// failure during a lookup must surface as a 5xx envelope via
/// ExceptionMiddleware, not the old `Ok(new { success = false, message })`
/// -- which looked identical to a normal "not found" 200 to any caller that
/// only checked the HTTP status.</summary>
public class LookupControllerTests
{
    private class ThrowingModule : IOrderModule
    {
        private readonly bool _branchLookup;

        public ThrowingModule(bool branchLookup = false)
        {
            _branchLookup = branchLookup;
        }

        public string Key => "throwing_module";
        public string Label => "Throwing Module";
        public string Client => "Test";
        public bool Available => true;
        public ModuleCapabilities Capabilities => new(
            DraftKind: "flat", ItemLookup: true, ConsumerLookup: true,
            OrderRequests: false, Cancel: false, Resend: false,
            BranchLookup: _branchLookup);

        public IReadOnlyDictionary<string, ModuleEnvironment> Environments { get; } = new Dictionary<string, ModuleEnvironment>
        {
            ["Test"] = new ModuleEnvironment
            {
                Key = "Test", Environment = "Testing", Description = "", Accent = "", Cue = "", Icon = "",
                RouteLabel = "", VisualUrl = "", VisualAlt = "", Available = true,
                ConnectionStringName = "ThrowingModuleTest"
            }
        };

        public ModuleEnvironment GetEnvironment(string? envKey) => Environments["Test"];
        public OrderDraft DefaultState() => new();
        public Dictionary<string, object?> BuildPayload(OrderDraft draft) => new();
        public List<string> Validate(OrderDraft draft) => new();

        public Task<Product?> LookupItemAsync(string connectionString, string code, string? branchCode = null) =>
            throw new InvalidOperationException("simulated database connection failure");

        public Task<Consumer?> LookupConsumerByPhoneAsync(string connectionString, string phone) =>
            throw new InvalidOperationException("simulated database connection failure");
    }

    private class SingleModuleRegistry : IModuleRegistry
    {
        private readonly IOrderModule _module;
        public SingleModuleRegistry(IOrderModule? module = null) => _module = module ?? new ThrowingModule();
        public IReadOnlyCollection<IOrderModule> GetAllModules() => new[] { _module };
        public IOrderModule? GetModule(string key) => key == _module.Key ? _module : null;
        public IOrderModule GetModuleOrThrow(string key) => GetModule(key) ?? throw new KeyNotFoundException(key);
    }

    private class FakeBranchRepository : IBranchRepository
    {
        public int CallCount;
        public List<BranchOptionDto> Branches = new()
        {
            new BranchOptionDto("101", "Main Branch"),
            new BranchOptionDto("P900", "Test Pharmacy")
        };

        public Task<List<BranchOptionDto>> ListBranchesAsync(string connectionString)
        {
            CallCount++;
            return Task.FromResult(Branches);
        }
    }

    private static IConfiguration BuildConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ThrowingModuleTest"] = "Server=fake;Database=fake;"
        })
        .Build();

    private static LookupController BuildController(IOrderModule module, IBranchRepository branches) =>
        new(new SingleModuleRegistry(module), BuildConfiguration(), branches, new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task LookupItem_DatabaseFailure_ThrowsUpstreamException_NotOk200()
    {
        var controller = BuildController(new ThrowingModule(), new FakeBranchRepository());

        var ex = await Assert.ThrowsAsync<UpstreamException>(() =>
            controller.LookupItem("throwing_module", code: "123"));

        Assert.Equal(502, ex.StatusCode);
        Assert.Contains("simulated database connection failure", ex.Message);
    }

    [Fact]
    public async Task LookupConsumer_DatabaseFailure_ThrowsUpstreamException_NotOk200()
    {
        var controller = BuildController(new ThrowingModule(), new FakeBranchRepository());

        var ex = await Assert.ThrowsAsync<UpstreamException>(() =>
            controller.LookupConsumer("throwing_module", phone: "0500000000"));

        Assert.Equal(502, ex.StatusCode);
        Assert.Contains("simulated database connection failure", ex.Message);
    }

    [Fact]
    public async Task ListBranches_WithoutBranchLookupCapability_Returns501()
    {
        var repository = new FakeBranchRepository();
        var controller = BuildController(new ThrowingModule(branchLookup: false), repository);

        var result = await controller.ListBranches("throwing_module");

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(501, objectResult.StatusCode);
        Assert.Equal(0, repository.CallCount);
    }

    [Fact]
    public async Task ListBranches_WithCapability_ReturnsRepositoryData()
    {
        var repository = new FakeBranchRepository();
        var controller = BuildController(new ThrowingModule(branchLookup: true), repository);

        var result = await controller.ListBranches("throwing_module");

        var ok = Assert.IsType<OkObjectResult>(result);
        // Direct controller tests bypass MVC's configured JSON options. The
        // HTTP response is camelCase, so apply the same naming policy here.
        var json = JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Assert.Contains("\"code\":\"101\"", json);
        Assert.Contains("\"name\":\"Main Branch\"", json);
        Assert.Contains("\"code\":\"P900\"", json);
        Assert.Equal(1, repository.CallCount);
    }

    [Fact]
    public async Task ListBranches_SecondCall_ServedFromCache()
    {
        var repository = new FakeBranchRepository();
        var controller = BuildController(new ThrowingModule(branchLookup: true), repository);

        await controller.ListBranches("throwing_module");
        var second = await controller.ListBranches("throwing_module");

        Assert.IsType<OkObjectResult>(second);
        Assert.Equal(1, repository.CallCount);
    }

    [Fact]
    public async Task ListBranches_Refresh_BypassesCache()
    {
        var repository = new FakeBranchRepository();
        var controller = BuildController(new ThrowingModule(branchLookup: true), repository);

        await controller.ListBranches("throwing_module");
        await controller.ListBranches("throwing_module", refresh: true);

        Assert.Equal(2, repository.CallCount);
    }

    [Fact]
    public async Task ListBranches_RepositoryFailure_ThrowsUpstreamException_NotOk200()
    {
        var controller = BuildController(new ThrowingModule(branchLookup: true), new ThrowingBranchRepository());

        var ex = await Assert.ThrowsAsync<UpstreamException>(() =>
            controller.ListBranches("throwing_module"));

        Assert.Equal(502, ex.StatusCode);
        Assert.Contains("simulated database connection failure", ex.Message);
    }

    private class ThrowingBranchRepository : IBranchRepository
    {
        public Task<List<BranchOptionDto>> ListBranchesAsync(string connectionString) =>
            throw new InvalidOperationException("simulated database connection failure");
    }
}
