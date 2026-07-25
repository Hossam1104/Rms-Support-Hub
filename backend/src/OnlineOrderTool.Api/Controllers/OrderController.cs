using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OnlineOrderTool.Core.DTOs;
using OnlineOrderTool.Core.Models;
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
    private readonly IFlatOrderPayloadBuilder _flatPayloadBuilder;
    private readonly IUniCommercePayloadBuilder _uniPayloadBuilder;
    private readonly IFlatOrderValidator _flatValidator;
    private readonly IUniCommerceValidator _uniValidator;
    private readonly IApiClient _apiClient;
    private readonly IOrderHistoryService _historyService;
    private readonly ISqlServerConnectionFactory _connectionFactory;
    private readonly IConfiguration _configuration;

    public OrderController(
        IModuleRegistry moduleRegistry,
        IDraftManager draftManager,
        IFlatOrderPayloadBuilder flatPayloadBuilder,
        IUniCommercePayloadBuilder uniPayloadBuilder,
        IFlatOrderValidator flatValidator,
        IUniCommerceValidator uniValidator,
        IApiClient apiClient,
        IOrderHistoryService historyService,
        ISqlServerConnectionFactory connectionFactory,
        IConfiguration configuration)
    {
        _moduleRegistry = moduleRegistry;
        _draftManager = draftManager;
        _flatPayloadBuilder = flatPayloadBuilder;
        _uniPayloadBuilder = uniPayloadBuilder;
        _flatValidator = flatValidator;
        _uniValidator = uniValidator;
        _apiClient = apiClient;
        _historyService = historyService;
        _connectionFactory = connectionFactory;
        _configuration = configuration;
    }

    [HttpGet("state")]
    public async Task<ActionResult> GetState(string key)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(key) ?? module.DefaultState();
        return Ok(draft);
    }

    [HttpPut("order-field")]
    public async Task<ActionResult> UpdateOrderField(string key, [FromBody] UpdateOrderFieldRequest request)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(key) ?? module.DefaultState();
        draft.OrderData[request.FieldName] = request.Value;
        await _draftManager.SaveDraftAsync(key, draft);

        return Ok(new { success = true, state = draft });
    }

    [HttpGet("calculate-totals")]
    public async Task<ActionResult> CalculateTotals(string key)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(key) ?? module.DefaultState();
        var totals = TotalsCalculator.Calculate(draft);

        return Ok(totals);
    }

    [HttpGet("export-json")]
    public async Task<ActionResult> ExportJson(string key)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(key) ?? module.DefaultState();
        var payload = BuildPayloadForModule(key, draft);

        return Ok(payload);
    }

    [HttpPost("load-default")]
    public async Task<ActionResult> LoadDefault(string key)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var defaultDraft = module.DefaultState();
        await _draftManager.SaveDraftAsync(key, defaultDraft);

        return Ok(new { success = true, state = defaultDraft });
    }

    [HttpPost("clear-all")]
    public async Task<ActionResult> ClearAll(string key)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var emptyDraft = module.DefaultState();
        await _draftManager.SaveDraftAsync(key, emptyDraft);

        return Ok(new { success = true, state = emptyDraft });
    }

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

        var draft = await _draftManager.LoadDraftAsync(key) ?? module.DefaultState();
        var payload = BuildPayloadForModule(key, draft);

        var validationErrors = key == "ghc_unicommerce"
            ? _uniValidator.ValidatePayload(payload)
            : _flatValidator.ValidatePayload(payload, key);

        if (validationErrors.Count > 0)
        {
            return BadRequest(new { success = false, errors = validationErrors });
        }

        var apiResult = await _apiClient.SendOrderAsync(targetUrl, payload);

        var orderCode = payload.GetValueOrDefault("order_code")?.ToString()
            ?? payload.GetValueOrDefault("ReferenceNumber")?.ToString()
            ?? "UNKNOWN";

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

        var historyEntry = new OrderHistoryEntry
        {
            OrderCode = orderCode,
            ModuleKey = key,
            EnvironmentKey = env.Key,
            ApiUrl = targetUrl,
            RequestPayloadJson = JsonSerializer.Serialize(payload, jsonOptions),
            ResponseStatusCode = apiResult.StatusCode,
            ResponseBodyJson = apiResult.ResponseText
        };

        await _historyService.AddEntryAsync(key, historyEntry);

        return Ok(new
        {
            success = apiResult.Success,
            statusCode = apiResult.StatusCode,
            responseText = apiResult.ResponseText,
            urlSent = apiResult.UrlSent,
            historyEntryId = historyEntry.Id
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

    [HttpPost("resend-order")]
    public async Task<ActionResult> ResendOrder(string key, [FromQuery] string newBranchCode, [FromBody] SendOrderRequest request)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(key) ?? module.DefaultState();
        draft.OrderData["branch_code"] = newBranchCode;
        await _draftManager.SaveDraftAsync(key, draft);

        return await SendRequest(key, request);
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

    private Dictionary<string, object?> BuildPayloadForModule(string moduleKey, OrderDraft draft)
    {
        return moduleKey switch
        {
            "upc_ecommerce" => _flatPayloadBuilder.BuildUpcPayload(draft),
            "ghc_unicommerce" => _uniPayloadBuilder.BuildInvoicePayload(new OnlineOrderTool.Core.Models.UniCommerceInvoice
            {
                ReferenceNumber = draft.OrderData.GetValueOrDefault("reference_number")?.ToString() ?? "",
                OnlineOrderNumber = draft.OrderData.GetValueOrDefault("online_order_number")?.ToString() ?? "",
                IsReturn = draft.OrderData.TryGetValue("is_return", out var r) && r is true,
                ParentReferenceNumber = draft.OrderData.GetValueOrDefault("parent_reference_number")?.ToString(),
                CustomerName = draft.OrderData.GetValueOrDefault("customer_name")?.ToString() ?? "AMAZON",
                PaidOnlineAmount = decimal.TryParse(draft.OrderData.GetValueOrDefault("paid_online_amount")?.ToString(), out var po) ? po : 0m,
                PaidWithPointsAmount = decimal.TryParse(draft.OrderData.GetValueOrDefault("paid_with_points_amount")?.ToString(), out var pp) ? pp : 0m,
                Consumer = draft.Consumer,
                Delivery = draft.Delivery,
                RowItems = draft.RowItems
            }),
            _ => _flatPayloadBuilder.BuildGhcPayload(draft)
        };
    }
}
