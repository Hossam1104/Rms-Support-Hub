using Microsoft.AspNetCore.Mvc;
using OnlineOrderTool.Core.DTOs;
using OnlineOrderTool.Core.Models;
using OnlineOrderTool.Core.Services;

namespace OnlineOrderTool.Api.Controllers;

[ApiController]
[Route("api/modules/{key}/order-history")]
public class HistoryController : ControllerBase
{
    private readonly IOrderHistoryService _historyService;
    private readonly IApiClient _apiClient;

    public HistoryController(IOrderHistoryService historyService, IApiClient apiClient)
    {
        _historyService = historyService;
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderHistoryEntry>>> GetHistory(string key)
    {
        var history = await _historyService.GetHistoryAsync(key);
        return Ok(history);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderHistoryEntry>> GetEntry(string key, Guid id)
    {
        var entry = await _historyService.GetEntryAsync(key, id);
        if (entry == null) return NotFound(new { error = $"Order history entry '{id}' not found." });

        return Ok(entry);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult> CancelFromHistory(string key, Guid id, [FromBody] CancelOrderRequest request)
    {
        var entry = await _historyService.GetEntryAsync(key, id);
        if (entry == null) return NotFound(new { error = $"Order history entry '{id}' not found." });

        var cancelPayload = new
        {
            order_number = request.OrderNumber,
            cancel_reason = request.CancelReason
        };

        var apiResult = await _apiClient.SendOrderAsync(entry.ApiUrl, cancelPayload);
        await _historyService.MarkCancelledAsync(key, id, apiResult.ResponseText);

        return Ok(new
        {
            success = apiResult.Success,
            statusCode = apiResult.StatusCode,
            responseText = apiResult.ResponseText
        });
    }
}
