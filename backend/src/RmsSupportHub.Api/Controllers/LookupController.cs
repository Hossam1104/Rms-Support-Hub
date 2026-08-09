using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RmsSupportHub.Api.Exceptions;
using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Repositories;

namespace RmsSupportHub.Api.Controllers;

[ApiController]
[Route("api/modules/{key}/lookup")]
public class LookupController : ControllerBase
{
    private static readonly TimeSpan BranchCacheTtl = TimeSpan.FromMinutes(5);

    private readonly IModuleRegistry _moduleRegistry;
    private readonly IConfiguration _configuration;
    private readonly IBranchRepository _branchRepository;
    private readonly IMemoryCache _cache;

    public LookupController(
        IModuleRegistry moduleRegistry,
        IConfiguration configuration,
        IBranchRepository branchRepository,
        IMemoryCache cache)
    {
        _moduleRegistry = moduleRegistry;
        _configuration = configuration;
        _branchRepository = branchRepository;
        _cache = cache;
    }

    /// <summary>No more moduleKey == "upc_ecommerce" branching to pick a
    /// repository (see remediation_plan.md B21) -- module.LookupItemAsync
    /// dispatches to whichever repository that module was constructed with,
    /// and the connection string is resolved from the active environment's
    /// own ConnectionStringName, not a module-key switch.</summary>
    [HttpGet("item")]
    public async Task<ActionResult> LookupItem(string key, [FromQuery] string code, [FromQuery] string? branchCode = null, [FromQuery] string? envKey = null)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { success = false, message = $"Unknown module '{key}'" });

        var guard = CapabilityGuard.Require(module, c => c.ItemLookup, "item-lookup");
        if (guard != null) return guard;

        var env = module.GetEnvironment(envKey);
        var connStr = GetConnectionString(env);

        try
        {
            var product = await module.LookupItemAsync(connStr, code, branchCode);

            if (product == null)
                return Ok(new { success = false, message = $"No item found for code '{code}' in database." });

            return Ok(new { success = true, data = product });
        }
        catch (Exception ex) when (ex is not ApiException)
        {
            throw new UpstreamException($"Database connection error: {ex.Message}");
        }
    }

    [HttpGet("consumer")]
    public async Task<ActionResult> LookupConsumer(string key, [FromQuery] string phone, [FromQuery] string? envKey = null)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { success = false, message = $"Unknown module '{key}'" });

        var guard = CapabilityGuard.Require(module, c => c.ConsumerLookup, "consumer-lookup");
        if (guard != null) return guard;

        var env = module.GetEnvironment(envKey);
        var connStr = GetConnectionString(env);

        try
        {
            var consumer = await module.LookupConsumerByPhoneAsync(connStr, phone);

            if (consumer == null)
                return Ok(new { success = false, message = $"No consumer found in database for phone '{phone}'." });

            return Ok(new { success = true, data = consumer });
        }
        catch (Exception ex) when (ex is not ApiException)
        {
            throw new UpstreamException($"SQL Server database connection error: {ex.Message}");
        }
    }

    /// <summary>U3: the real branch list from
    /// dbo.Branches (BranchRepository), replacing the order-history GROUP BY
    /// that used to live on OrderRequestsController. Absolute route
    /// (~/api/modules/{key}/branches) so the picker URL is not nested under
    /// /lookup -- it serves the order builder and Order Requests alike.
    /// Gated on Capabilities.BranchLookup, not a module-key comparison.
    /// Cached per connection string for 5 minutes -- a branch list does not
    /// change during a session; refresh=true bypasses the cache explicitly.</summary>
    [HttpGet("~/api/modules/{key}/branches")]
    public async Task<ActionResult> ListBranches(string key, [FromQuery] string? envKey = null, [FromQuery] bool refresh = false)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { success = false, message = $"Unknown module '{key}'" });

        var guard = CapabilityGuard.Require(module, c => c.BranchLookup, "branches");
        if (guard != null) return guard;

        var env = module.GetEnvironment(envKey);
        var connStr = GetConnectionString(env);

        var cacheKey = $"branches:{connStr}";
        if (!refresh && _cache.TryGetValue(cacheKey, out List<Core.DTOs.BranchOptionDto>? cached) && cached != null)
        {
            return Ok(new { success = true, data = cached });
        }

        try
        {
            var branches = await _branchRepository.ListBranchesAsync(connStr);
            _cache.Set(cacheKey, branches, BranchCacheTtl);
            return Ok(new { success = true, data = branches });
        }
        catch (Exception ex) when (ex is not ApiException)
        {
            throw new UpstreamException($"Database connection error: {ex.Message}");
        }
    }

    private string GetConnectionString(Core.Models.ModuleEnvironment env)
    {
        return ConnectionStringResolver.RequireForEnvironment(_configuration, env);
    }
}
