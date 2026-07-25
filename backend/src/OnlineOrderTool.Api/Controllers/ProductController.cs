using Microsoft.AspNetCore.Mvc;
using OnlineOrderTool.Core.Models;
using OnlineOrderTool.Core.Modules;
using OnlineOrderTool.Core.Services;

namespace OnlineOrderTool.Api.Controllers;

[ApiController]
[Route("api/modules/{key}/products")]
public class ProductController : ControllerBase
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IDraftManager _draftManager;

    public ProductController(IModuleRegistry moduleRegistry, IDraftManager draftManager)
    {
        _moduleRegistry = moduleRegistry;
        _draftManager = draftManager;
    }

    [HttpPost]
    public async Task<ActionResult> AddProduct(string key, [FromBody] Product product)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(key) ?? module.DefaultState();
        draft.Products.Add(product);
        await _draftManager.SaveDraftAsync(key, draft);

        return Ok(new { success = true, products = draft.Products, totals = TotalsCalculator.Calculate(draft) });
    }

    [HttpPut("{index:int}")]
    public async Task<ActionResult> UpdateProduct(string key, int index, [FromBody] Product product)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(key) ?? module.DefaultState();
        if (index < 0 || index >= draft.Products.Count)
            return BadRequest(new { error = "Invalid product index." });

        draft.Products[index] = product;
        await _draftManager.SaveDraftAsync(key, draft);

        return Ok(new { success = true, products = draft.Products, totals = TotalsCalculator.Calculate(draft) });
    }

    [HttpDelete("{index:int}")]
    public async Task<ActionResult> DeleteProduct(string key, int index)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(key) ?? module.DefaultState();
        if (index < 0 || index >= draft.Products.Count)
            return BadRequest(new { error = "Invalid product index." });

        draft.Products.RemoveAt(index);
        await _draftManager.SaveDraftAsync(key, draft);

        return Ok(new { success = true, products = draft.Products, totals = TotalsCalculator.Calculate(draft) });
    }
}
