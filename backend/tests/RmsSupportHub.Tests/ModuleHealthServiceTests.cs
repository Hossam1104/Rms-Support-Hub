using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Services;
using RmsSupportHub.Data;
using RmsSupportHub.Data.Repositories;
using Xunit;

namespace RmsSupportHub.Tests;

public class ModuleHealthServiceTests
{
    private static ModuleRegistry BuildRegistry() => new(
        new FlatOrderPayloadBuilder(),
        new FlatOrderValidator(),
        new UniCommercePayloadBuilder(),
        new UniCommerceValidator(),
        new FlatOrderItemRepository(new SqlServerConnectionFactory()),
        new GhcConsumerRepository(new SqlServerConnectionFactory()),
        new UpcItemRepository(new SqlServerConnectionFactory()),
        new UpcConsumerRepository(new SqlServerConnectionFactory()),
        TestEnvironmentCatalog.UpcOnly());

    /// <summary>Records every probed URL so a test can prove the probe never
    /// sends a payload and never touches an endpoint it was not given.</summary>
    private class ProbeApiClient : IApiClient
    {
        private readonly Func<string, bool> _reachable;
        public readonly List<string> ProbedUrls = new();
        public int SendCallCount;

        public ProbeApiClient(Func<string, bool>? reachable = null)
        {
            _reachable = reachable ?? (_ => true);
        }

        public Task<ApiResponseResult> SendOrderAsync(string url, object payloadJson)
        {
            SendCallCount++;
            return Task.FromResult(new ApiResponseResult(200, "{}", url, true));
        }

        public Task<bool> TestEndpointAsync(string url, TimeSpan? timeout = null)
        {
            ProbedUrls.Add(url);
            return Task.FromResult(_reachable(url));
        }
    }

    [Fact]
    public async Task GetHealthAsync_ReportsEveryEnvironmentAndNeverSendsAPayload()
    {
        var registry = BuildRegistry();
        var apiClient = new ProbeApiClient();
        var service = new ModuleHealthService(
            registry,
            new EnvironmentPolicy(DeploymentTier.Testing),
            apiClient,
            new ModuleHealthCache());

        var health = await service.GetHealthAsync();

        var expected = registry.GetAllModules().Sum(m => m.Environments.Count);
        Assert.Equal(expected, health.Count);
        Assert.All(health, entry => Assert.False(string.IsNullOrWhiteSpace(entry.ModuleKey)));
        // Reachability is a connect probe only: nothing may be posted upstream.
        Assert.Equal(0, apiClient.SendCallCount);
    }

    [Fact]
    public async Task GetHealthAsync_MarksUnreachableAndUnconfiguredEnvironmentsDistinctly()
    {
        var registry = BuildRegistry();
        // Production is configured in the fixture but the Testing deployment
        // policy must disable it before the probe client sees its URL.
        var apiClient = new ProbeApiClient(_ => false);
        var service = new ModuleHealthService(
            registry,
            new EnvironmentPolicy(DeploymentTier.Testing),
            apiClient,
            new ModuleHealthCache());

        var health = await service.GetHealthAsync();

        var upcProduction = health.Single(h => h.EnvironmentKey == "UPC Production");
        Assert.Equal(ModuleHealthService.StatusPolicyDisabled, upcProduction.Status);

        var upcTesting = health.Single(h => h.EnvironmentKey == "UPC Testing");
        Assert.Equal(ModuleHealthService.StatusUnreachable, upcTesting.Status);

        var unicommerce = health.Where(h => h.ModuleKey == "ghc_unicommerce").ToList();
        Assert.NotEmpty(unicommerce);
        Assert.Equal(
            ModuleHealthService.StatusPolicyDisabled,
            unicommerce.Single(entry => entry.EnvironmentKey.EndsWith("Production", StringComparison.Ordinal)).Status);
        Assert.Equal(
            ModuleHealthService.StatusUnconfigured,
            unicommerce.Single(entry => entry.EnvironmentKey.EndsWith("Testing", StringComparison.Ordinal)).Status);
        // An environment with no configured endpoint must never be probed.
        Assert.DoesNotContain(apiClient.ProbedUrls, url => string.IsNullOrWhiteSpace(url));
        Assert.DoesNotContain(apiClient.ProbedUrls, url => !url.Contains(":8080", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetHealthAsync_ServesTheCachedSweepInsteadOfReprobing()
    {
        var registry = BuildRegistry();
        var apiClient = new ProbeApiClient();
        var cache = new ModuleHealthCache();
        var service = new ModuleHealthService(
            registry,
            new EnvironmentPolicy(DeploymentTier.Testing),
            apiClient,
            cache);

        await service.GetHealthAsync();
        var afterFirstSweep = apiClient.ProbedUrls.Count;
        await service.GetHealthAsync();

        Assert.True(afterFirstSweep > 0);
        Assert.Equal(afterFirstSweep, apiClient.ProbedUrls.Count);
    }

    /// <summary>The lane label is config, the probe is observation. Overloading
    /// one onto the other would let a failing probe make a Production lane stop
    /// announcing itself as Live.</summary>
    [Fact]
    public async Task GetHealthAsync_LeavesTheLaneStatusLabelUntouched()
    {
        var registry = BuildRegistry();
        var service = new ModuleHealthService(
            registry,
            new EnvironmentPolicy(DeploymentTier.Testing),
            new ProbeApiClient(_ => false),
            new ModuleHealthCache());

        await service.GetHealthAsync();

        var upcProduction = registry.GetModule("upc_ecommerce")!.Environments["UPC Production"];
        Assert.Equal("Live", upcProduction.StatusLabel);
    }
}
