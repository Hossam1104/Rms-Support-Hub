using Microsoft.AspNetCore.Mvc;
using OnlineOrderTool.Api.Middleware;
using OnlineOrderTool.Core.DTOs;
using OnlineOrderTool.Core.Modules;
using OnlineOrderTool.Core.Services;
using OnlineOrderTool.Data;

namespace OnlineOrderTool.Api.Controllers;

[ApiController]
[Route("api/modules/{key}")]
public class OrderController : ControllerBase
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IDraftManager _draftManager;
    private readonly IApiClient _apiClient;
    private readonly ISqlServerConnectionFactory _connectionFactory;

    public OrderController(
        IModuleRegistry moduleRegistry,
        IDraftManager draftManager,
        IApiClient apiClient,
        ISqlServerConnectionFactory connectionFactory)
    {
        _moduleRegistry = moduleRegistry;
        _draftManager = draftManager;
        _apiClient = apiClient;
        _connectionFactory = connectionFactory;
    }

    [HttpGet("state")]
    public async Task<ActionResult> GetState(string key)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(HttpContext.GetSessionId(), key) ?? module.DefaultState();
        return Ok(draft);
    }

    [HttpPut("order-field")]
    public async Task<ActionResult> UpdateOrderField(string key, [FromBody] UpdateOrderFieldRequest request)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(HttpContext.GetSessionId(), key) ?? module.DefaultState();
        draft.OrderData[request.FieldName] = request.Value;
        await _draftManager.SaveDraftAsync(HttpContext.GetSessionId(), key, draft);

        return Ok(new { success = true, state = draft });
    }

    [HttpGet("calculate-totals")]
    public async Task<ActionResult> CalculateTotals(string key)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(HttpContext.GetSessionId(), key) ?? module.DefaultState();
        var totals = TotalsCalculator.Calculate(draft);

        return Ok(totals);
    }

    [HttpGet("export-json")]
    public async Task<ActionResult> ExportJson(string key)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(HttpContext.GetSessionId(), key) ?? module.DefaultState();
        return Ok(module.BuildPayload(draft));
    }

    [HttpPost("load-default")]
    public async Task<ActionResult> LoadDefault(string key)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var defaultDraft = module.DefaultState();
        await _draftManager.SaveDraftAsync(HttpContext.GetSessionId(), key, defaultDraft);

        return Ok(new { success = true, state = defaultDraft });
    }

    [HttpPost("clear-all")]
    public async Task<ActionResult> ClearAll(string key)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var emptyDraft = module.DefaultState();
        await _draftManager.SaveDraftAsync(HttpContext.GetSessionId(), key, emptyDraft);

        return Ok(new { success = true, state = emptyDraft });
    }

    /// <summary>No more module-key switch to pick a builder/validator (see
    /// remediation_plan.md B21) -- module.BuildPayload/Validate are now
    /// fully self-sufficient (each module owns its own payload builder,
    /// validator and totals calculation internally).</summary>
    [HttpPost("send-request")]
    public async Task<ActionResult> SendRequest(string key, [FromBody] SendOrderRequest request)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var env = module.GetEnvironment(request.EnvironmentKey);
        var targetUrl = !string.IsNullOrWhiteSpace(request.CustomApiUrl) ? request.CustomApiUrl : env.ApiUrl;

        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return BadRequest(new { error = $"No API URL available for environment '{env.Key}' in module '{key}'." });
        }

        var draft = await _draftManager.LoadDraftAsync(HttpContext.GetSessionId(), key) ?? module.DefaultState();
        var validationErrors = module.Validate(draft);

        if (validationErrors.Count > 0)
        {
            return BadRequest(new { success = false, errors = validationErrors });
        }

        var payload = module.BuildPayload(draft);
        var apiResult = await _apiClient.SendOrderAsync(targetUrl, payload);

        // The sent request/response is not saved locally -- it is already
        // recorded server-side in the OrderRequests table (see R4's
        // OrderRequestRepository and OrderRequestsController), which is the
        // source of truth for the Order Requests page.
        return Ok(new
        {
            success = apiResult.Success,
            statusCode = apiResult.StatusCode,
            responseText = apiResult.ResponseText,
            urlSent = apiResult.UrlSent
        });
    }

    [HttpPost("cancel-order")]
    public async Task<ActionResult> CancelOrder(string key, [FromBody] CancelOrderRequest request)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var env = module.GetEnvironment(null);
        if (string.IsNullOrWhiteSpace(env.CancelUrl))
        {
            return BadRequest(new { error = $"Cancellation URL is not configured for environment '{env.Key}'." });
        }

        var cancelPayload = new
        {
            order_number = request.OrderNumber,
            cancel_reason = request.CancelReason
        };

        var apiResult = await _apiClient.SendOrderAsync(env.CancelUrl, cancelPayload);

        return Ok(new
        {
            success = apiResult.Success,
            statusCode = apiResult.StatusCode,
            responseText = apiResult.ResponseText
        });
    }

    [HttpPost("test-endpoint")]
    public async Task<ActionResult> TestEndpoint([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return BadRequest(new { error = "URL parameter is required." });

        var isOnline = await _apiClient.TestEndpointAsync(url);
        return Ok(new { status = isOnline ? "Online" : "Offline", url });
    }

    [HttpPost("test-db")]
    public ActionResult TestDb([FromQuery] string connectionString)
    {
        try
        {
            using var conn = _connectionFactory.CreateConnection(connectionString);
            return Ok(new { status = "Online", database = conn.Database });
        }
        catch (Exception ex)
        {
            return Ok(new { status = "Offline", error = ex.Message });
        }
    }
}
