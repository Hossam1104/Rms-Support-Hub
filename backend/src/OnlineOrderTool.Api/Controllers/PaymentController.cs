using Microsoft.AspNetCore.Mvc;
using OnlineOrderTool.Api.Middleware;
using OnlineOrderTool.Core.Models;
using OnlineOrderTool.Core.Modules;
using OnlineOrderTool.Core.Services;

namespace OnlineOrderTool.Api.Controllers;

[ApiController]
[Route("api/modules/{key}/payments")]
public class PaymentController : ControllerBase
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IDraftManager _draftManager;
    private readonly IFlatOrderValidator _flatValidator;

    public PaymentController(IModuleRegistry moduleRegistry, IDraftManager draftManager, IFlatOrderValidator flatValidator)
    {
        _moduleRegistry = moduleRegistry;
        _draftManager = draftManager;
        _flatValidator = flatValidator;
    }

    [HttpPost]
    public async Task<ActionResult> AddPayment(string key, [FromBody] Payment payment)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(HttpContext.GetSessionId(), key) ?? module.DefaultState();

        if (key == "upc_ecommerce" && payment.PaymentMethod == "PostToCredit")
        {
            return BadRequest(new { error = "PostToCredit payment method is not allowed for UPC E-Commerce." });
        }

        draft.Payments.Add(payment);
        await _draftManager.SaveDraftAsync(HttpContext.GetSessionId(), key, draft);

        return Ok(new { success = true, payments = draft.Payments, totals = TotalsCalculator.Calculate(draft) });
    }

    [HttpPut("{index:int}")]
    public async Task<ActionResult> UpdatePayment(string key, int index, [FromBody] Payment payment)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(HttpContext.GetSessionId(), key) ?? module.DefaultState();
        if (index < 0 || index >= draft.Payments.Count)
            return BadRequest(new { error = "Invalid payment index." });

        if (key == "upc_ecommerce" && payment.PaymentMethod == "PostToCredit")
        {
            return BadRequest(new { error = "PostToCredit payment method is not allowed for UPC E-Commerce." });
        }

        draft.Payments[index] = payment;
        await _draftManager.SaveDraftAsync(HttpContext.GetSessionId(), key, draft);

        return Ok(new { success = true, payments = draft.Payments, totals = TotalsCalculator.Calculate(draft) });
    }

    [HttpDelete("{index:int}")]
    public async Task<ActionResult> DeletePayment(string key, int index)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(HttpContext.GetSessionId(), key) ?? module.DefaultState();
        if (index < 0 || index >= draft.Payments.Count)
            return BadRequest(new { error = "Invalid payment index." });

        draft.Payments.RemoveAt(index);
        await _draftManager.SaveDraftAsync(HttpContext.GetSessionId(), key, draft);

        return Ok(new { success = true, payments = draft.Payments, totals = TotalsCalculator.Calculate(draft) });
    }
}
