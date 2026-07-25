using Microsoft.AspNetCore.Mvc;
using OnlineOrderTool.Core.DTOs;
using OnlineOrderTool.Core.Modules;
using OnlineOrderTool.Data.Repositories;

namespace OnlineOrderTool.Api.Controllers;

[ApiController]
[Route("api/modules/{key}/validation")]
public class ValidationController : ControllerBase
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IOrderValidationRepository _validationRepository;
    private readonly IConfiguration _configuration;

    public ValidationController(
        IModuleRegistry moduleRegistry,
        IOrderValidationRepository validationRepository,
        IConfiguration configuration)
    {
        _moduleRegistry = moduleRegistry;
        _validationRepository = validationRepository;
        _configuration = configuration;
    }

    [HttpPost("search")]
    public async Task<ActionResult> SearchOrders(string key, [FromBody] OrderSearchRequest filters, [FromQuery] string? envKey = null)
    {
        if (key != "upc_ecommerce") return BadRequest(new { error = "Order validation search is only supported for UPC E-Commerce." });

        var connStr = GetConnectionString(envKey);
        try
        {
            var results = await _validationRepository.SearchOrdersAsync(connStr, filters);
            return Ok(new { success = true, count = results.Count(), results });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("order/{orderNumber}")]
    public async Task<ActionResult> GetOrderDetails(string key, string orderNumber, [FromQuery] long? headerId = null, [FromQuery] string? envKey = null)
    {
        if (key != "upc_ecommerce") return BadRequest(new { error = "Order validation details are only supported for UPC E-Commerce." });

        var connStr = GetConnectionString(envKey);
        try
        {
            var details = await _validationRepository.GetOrderDetailsAsync(connStr, orderNumber, headerId);
            if (details == null) return NotFound(new { success = false, message = $"Order '{orderNumber}' not found." });

            var rawJson = await _validationRepository.GetLatestRequestJsonAsync(connStr, orderNumber);
            details["latestRequestJson"] = rawJson;

            return Ok(new { success = true, data = details });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private string GetConnectionString(string? envKey)
    {
        var name = envKey == "UPC Production" ? "UpcEcommerceProd" : "UpcEcommerceTest";
        return ConnectionStringResolver.Require(_configuration, name);
    }
}
