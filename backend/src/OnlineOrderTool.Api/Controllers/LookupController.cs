using Microsoft.AspNetCore.Mvc;
using OnlineOrderTool.Core.Modules;
using OnlineOrderTool.Data.Repositories;

namespace OnlineOrderTool.Api.Controllers;

[ApiController]
[Route("api/modules/{key}/lookup")]
public class LookupController : ControllerBase
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly FlatOrderItemRepository _flatItemRepo;
    private readonly UpcItemRepository _upcItemRepo;
    private readonly GhcConsumerRepository _ghcConsumerRepo;
    private readonly UpcConsumerRepository _upcConsumerRepo;
    private readonly IConfiguration _configuration;

    public LookupController(
        IModuleRegistry moduleRegistry,
        FlatOrderItemRepository flatItemRepo,
        UpcItemRepository upcItemRepo,
        GhcConsumerRepository ghcConsumerRepo,
        UpcConsumerRepository upcConsumerRepo,
        IConfiguration configuration)
    {
        _moduleRegistry = moduleRegistry;
        _flatItemRepo = flatItemRepo;
        _upcItemRepo = upcItemRepo;
        _ghcConsumerRepo = ghcConsumerRepo;
        _upcConsumerRepo = upcConsumerRepo;
        _configuration = configuration;
    }

    [HttpGet("item")]
    public async Task<ActionResult> LookupItem(string key, [FromQuery] string code, [FromQuery] string? branchCode = null, [FromQuery] string? envKey = null)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { success = false, message = $"Unknown module '{key}'" });

        var env = module.GetEnvironment(envKey);
        var connStr = GetConnectionString(key, env.Key);

        try
        {
            var product = key switch
            {
                "upc_ecommerce" => await _upcItemRepo.LookupItemAsync(connStr, code, branchCode),
                _ => await _flatItemRepo.LookupItemAsync(connStr, code, branchCode)
            };

            if (product == null)
                return Ok(new { success = false, message = $"No item found for code '{code}' in database." });

            return Ok(new { success = true, data = product });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"Database connection error: {ex.Message}" });
        }
    }

    [HttpGet("consumer")]
    public async Task<ActionResult> LookupConsumer(string key, [FromQuery] string phone, [FromQuery] string? envKey = null)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { success = false, message = $"Unknown module '{key}'" });

        var env = module.GetEnvironment(envKey);
        var connStr = GetConnectionString(key, env.Key);

        try
        {
            var consumer = key switch
            {
                "upc_ecommerce" => await _upcConsumerRepo.LookupConsumerByPhoneAsync(connStr, phone),
                _ => await _ghcConsumerRepo.LookupConsumerByPhoneAsync(connStr, phone)
            };

            if (consumer == null)
                return Ok(new { success = false, message = $"No consumer found in database for phone '{phone}'." });

            return Ok(new { success = true, data = consumer });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"SQL Server database connection error: {ex.Message}" });
        }
    }

    private string GetConnectionString(string moduleKey, string envKey)
    {
        if (moduleKey == "upc_ecommerce")
        {
            var name = envKey == "UPC Production" ? "UpcEcommerceProd" : "UpcEcommerceTest";
            var baseConnStr = ConnectionStringResolver.Require(_configuration, name);

            if (!baseConnStr.Contains("Connect Timeout", StringComparison.OrdinalIgnoreCase))
            {
                baseConnStr += ";Connect Timeout=5;";
            }
            return baseConnStr;
        }

        var ghcConnStr = ConnectionStringResolver.Require(_configuration, "GhcEcommerce");
        if (!ghcConnStr.Contains("Connect Timeout", StringComparison.OrdinalIgnoreCase))
        {
            ghcConnStr += ";Connect Timeout=5;";
        }
        return ghcConnStr;
    }
}
