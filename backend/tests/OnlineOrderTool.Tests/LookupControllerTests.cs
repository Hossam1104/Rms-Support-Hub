using Microsoft.Extensions.Configuration;
using OnlineOrderTool.Api.Controllers;
using OnlineOrderTool.Api.Exceptions;
using OnlineOrderTool.Core.Models;
using OnlineOrderTool.Core.Modules;
using Xunit;

namespace OnlineOrderTool.Tests;

/// <summary>Proves the R6 fix for remediation_plan.md B22: a database
/// failure during a lookup must surface as a 5xx envelope via
/// ExceptionMiddleware, not the old `Ok(new { success = false, message })`
/// -- which looked identical to a normal "not found" 200 to any caller that
/// only checked the HTTP status.</summary>
public class LookupControllerTests
{
    private class ThrowingModule : IOrderModule
    {
        public string Key => "throwing_module";
        public string Label => "Throwing Module";
        public string Client => "Test";
        public bool Available => true;
        public ModuleCapabilities Capabilities { get; } = new(
            DraftKind: "flat", ItemLookup: true, ConsumerLookup: true,
            OrderRequests: false, Cancel: false, Resend: false);

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
        private readonly IOrderModule _module = new ThrowingModule();
        public IReadOnlyCollection<IOrderModule> GetAllModules() => new[] { _module };
        public IOrderModule? GetModule(string key) => key == _module.Key ? _module : null;
        public IOrderModule GetModuleOrThrow(string key) => GetModule(key) ?? throw new KeyNotFoundException(key);
    }

    private static IConfiguration BuildConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ThrowingModuleTest"] = "Server=fake;Database=fake;"
        })
        .Build();

    [Fact]
    public async Task LookupItem_DatabaseFailure_ThrowsUpstreamException_NotOk200()
    {
        var controller = new LookupController(new SingleModuleRegistry(), BuildConfiguration());

        var ex = await Assert.ThrowsAsync<UpstreamException>(() =>
            controller.LookupItem("throwing_module", code: "123"));

        Assert.Equal(502, ex.StatusCode);
        Assert.Contains("simulated database connection failure", ex.Message);
    }

    [Fact]
    public async Task LookupConsumer_DatabaseFailure_ThrowsUpstreamException_NotOk200()
    {
        var controller = new LookupController(new SingleModuleRegistry(), BuildConfiguration());

        var ex = await Assert.ThrowsAsync<UpstreamException>(() =>
            controller.LookupConsumer("throwing_module", phone: "0500000000"));

        Assert.Equal(502, ex.StatusCode);
        Assert.Contains("simulated database connection failure", ex.Message);
    }
}
