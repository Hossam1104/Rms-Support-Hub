using Microsoft.AspNetCore.Mvc;
using OnlineOrderTool.Api.Exceptions;
using OnlineOrderTool.Core.Modules;

namespace OnlineOrderTool.Api.Controllers;

[ApiController]
[Route("api/modules/{key}/lookup")]
public class LookupController : ControllerBase
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IConfiguration _configuration;

    public LookupController(IModuleRegistry moduleRegistry, IConfiguration configuration)
    {
        _moduleRegistry = moduleRegistry;
        _configuration = configuration;
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

    private string GetConnectionString(Core.Models.ModuleEnvironment env)
    {
        var name = env.ConnectionStringName
            ?? throw new ConfigurationException($"Environment '{env.Key}' has no ConnectionStringName configured.");
        var connStr = ConnectionStringResolver.Require(_configuration, name);

        if (!connStr.Contains("Connect Timeout", StringComparison.OrdinalIgnoreCase))
        {
            connStr += ";Connect Timeout=5;";
        }
        return connStr;
    }
}
