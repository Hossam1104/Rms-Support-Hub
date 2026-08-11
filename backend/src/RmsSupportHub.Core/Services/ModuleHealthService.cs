using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Modules;

namespace RmsSupportHub.Core.Services;

/// <summary>Reachability of one module environment's send endpoint, as observed
/// from the API host. This is deliberately separate from
/// ModuleEnvironment.StatusLabel: that label says which lane an environment is
/// (Live/Test/Soon) and must keep saying so even while the host is down, or a
/// failing probe would read as "not production" and invite a send against the
/// live lane.</summary>
public record EnvironmentHealth(string ModuleKey, string EnvironmentKey, string Status, DateTimeOffset CheckedAt);

public interface IModuleHealthService
{
    Task<IReadOnlyList<EnvironmentHealth>> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>Process-wide snapshot of the last probe sweep. Held separately from
/// the service so the service itself can stay transient alongside the typed
/// HttpClient it depends on, without capturing it in a singleton.</summary>
public class ModuleHealthCache
{
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private IReadOnlyList<EnvironmentHealth>? _snapshot;
    private DateTimeOffset _expiresAt;

    public ModuleHealthCache(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Repeated dashboard loads must not re-probe internal hosts on
    /// every render; a stale-by-seconds reading is the right trade.</summary>
    public TimeSpan Ttl { get; init; } = TimeSpan.FromSeconds(30);

    public IReadOnlyList<EnvironmentHealth>? Read()
    {
        lock (_gate)
        {
            return _snapshot is not null && _timeProvider.GetUtcNow() < _expiresAt ? _snapshot : null;
        }
    }

    public void Write(IReadOnlyList<EnvironmentHealth> snapshot)
    {
        lock (_gate)
        {
            _snapshot = snapshot;
            _expiresAt = _timeProvider.GetUtcNow().Add(Ttl);
        }
    }
}

/// <summary>
/// Probes every configured environment endpoint with a TCP connect only -- no
/// HTTP request and no payload, because the upstream URLs are POST-only order
/// operations (CreateAndAssignOrder/CancelOrder) with no health route. A
/// successful probe therefore proves a listener is accepting connections, not
/// that the API is healthy; the status name says "reachable" for that reason.
/// </summary>
public class ModuleHealthService : IModuleHealthService
{
    public const string StatusReachable = "reachable";
    public const string StatusUnreachable = "unreachable";
    public const string StatusUnconfigured = "unconfigured";

    private readonly IModuleRegistry _moduleRegistry;
    private readonly IApiClient _apiClient;
    private readonly ModuleHealthCache _cache;
    private readonly TimeProvider _timeProvider;

    public ModuleHealthService(
        IModuleRegistry moduleRegistry,
        IApiClient apiClient,
        ModuleHealthCache cache,
        TimeProvider? timeProvider = null)
    {
        _moduleRegistry = moduleRegistry;
        _apiClient = apiClient;
        _cache = cache;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<EnvironmentHealth>> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var cached = _cache.Read();
        if (cached is not null) return cached;

        var checkedAt = _timeProvider.GetUtcNow();

        // One probe per environment, all in flight together: a single
        // unreachable host must not serialize the whole dashboard behind its
        // connect timeout.
        var probes = _moduleRegistry.GetAllModules()
            .SelectMany(module => module.Environments.Values.Select(environment => (module.Key, environment)))
            .Select(pair => ProbeAsync(pair.Key, pair.environment, checkedAt, cancellationToken))
            .ToList();

        IReadOnlyList<EnvironmentHealth> results = await Task.WhenAll(probes);
        _cache.Write(results);
        return results;
    }

    private async Task<EnvironmentHealth> ProbeAsync(
        string moduleKey,
        ModuleEnvironment environment,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(environment.ApiUrl))
        {
            return new EnvironmentHealth(moduleKey, environment.Key, StatusUnconfigured, checkedAt);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var reachable = await _apiClient.TestEndpointAsync(environment.ApiUrl);
        return new EnvironmentHealth(
            moduleKey,
            environment.Key,
            reachable ? StatusReachable : StatusUnreachable,
            checkedAt);
    }
}
