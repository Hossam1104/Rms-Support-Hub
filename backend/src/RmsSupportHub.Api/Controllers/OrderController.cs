using Microsoft.AspNetCore.Mvc;
using RmsSupportHub.Api.Exceptions;
using RmsSupportHub.Api.Middleware;
using RmsSupportHub.Core.DTOs;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Services;
using RmsSupportHub.Data;

namespace RmsSupportHub.Api.Controllers;

[ApiController]
[Route("api/modules/{key}")]
public class OrderController : ControllerBase
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IDraftManager _draftManager;
    private readonly IApiClient _apiClient;
    private readonly ISqlServerConnectionFactory _connectionFactory;
    private readonly IConnectionStringResolver _connectionStrings;
    private readonly IEnvironmentPolicy _environmentPolicy;

    public OrderController(
        IModuleRegistry moduleRegistry,
        IDraftManager draftManager,
        IApiClient apiClient,
        ISqlServerConnectionFactory connectionFactory,
        IConnectionStringResolver connectionStrings,
        IEnvironmentPolicy environmentPolicy)
    {
        _moduleRegistry = moduleRegistry;
        _draftManager = draftManager;
        _apiClient = apiClient;
        _connectionFactory = connectionFactory;
        _connectionStrings = connectionStrings;
        _environmentPolicy = environmentPolicy;
    }

    [HttpGet("state")]
    public async Task<ActionResult> GetState(string key)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(HttpContext.GetSessionId(), key) ?? module.DefaultState();
        return Ok(draft);
    }

    /// <summary>Persists the complete module draft for workflows whose state
    /// is not represented by OrderData alone, such as Uni-Commerce row items,
    /// consumer details, and delivery details. The browser still receives the
    /// same per-session isolation as the field patch route.</summary>
    [HttpPut("state")]
    public async Task<ActionResult> PutState(string key, [FromBody] OrderDraft draft)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        await _draftManager.SaveDraftAsync(HttpContext.GetSessionId(), key, draft);
        return Ok(new { success = true, state = draft });
    }

    /// <summary>U4 (UI_Rework_Plan.md D13): the active environment's resolved
    /// send endpoint for display in the API configuration area. Scoped and
    /// additive -- GET /api/modules still never carries URLs (B16); this
    /// exposes only the single resolved environment's ApiUrl, already
    /// disclosed post-send as urlSent. Uses the same GetEnvironment
    /// resolution as send-request.</summary>
    [HttpGet("endpoint")]
    public ActionResult GetEndpoint(string key, [FromQuery] string? envKey = null)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var env = CapabilityGuard.RequireEnvironment(_environmentPolicy, module, envKey);
        return Ok(new ModuleEndpointDto(env.Key, env.Environment, env.ApiUrl));
    }

    /// <summary>U2 (UI_Rework_Plan.md D1): applies every field in one
    /// synchronised load-modify-write via DraftManager.PatchOrderDataAsync,
    /// replacing the per-field PUT order-field race -- a consumer lookup
    /// that used to fire eight separate requests now sends one.</summary>
    [HttpPatch("order-data")]
    public async Task<ActionResult> PatchOrderData(string key, [FromBody] PatchOrderDataRequest request)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        if (request.Fields == null || request.Fields.Count == 0)
        {
            return BadRequest(new { error = "At least one field is required." });
        }

        var draft = await _draftManager.PatchOrderDataAsync(HttpContext.GetSessionId(), key, request.Fields, module.DefaultState);
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

        var env = CapabilityGuard.RequireEnvironment(_environmentPolicy, module, request.EnvironmentKey);
        var targetUrl = env.ApiUrl;

        if (string.IsNullOrWhiteSpace(targetUrl))
            throw new EnvironmentUnconfiguredException();

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

        var capabilityGuard = CapabilityGuard.Require(module, c => c.Cancel, "cancel");
        if (capabilityGuard != null) return capabilityGuard;

        var env = CapabilityGuard.RequireEnvironment(_environmentPolicy, module, request.EnvironmentKey);
        if (string.IsNullOrWhiteSpace(env.CancelUrl))
            throw new EnvironmentUnconfiguredException();

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
    public async Task<ActionResult> TestEndpoint(string key, [FromQuery] string? envKey = null)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var environment = CapabilityGuard.RequireEnvironment(_environmentPolicy, module, envKey);
        if (!environment.HealthProbeEnabled || string.IsNullOrWhiteSpace(environment.ApiUrl))
            throw new EnvironmentUnconfiguredException();

        var isOnline = await _apiClient.TestEndpointAsync(environment.ApiUrl, environment.HealthProbeTimeout);
        if (!isOnline) throw new DownstreamUnreachableException();
        return Ok(new { status = "Online" });
    }

    [HttpPost("test-db")]
    public ActionResult TestDb(string key, [FromQuery] string? envKey = null)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var environment = CapabilityGuard.RequireEnvironment(_environmentPolicy, module, envKey);
        if (!environment.RequiresDatabase)
            return new ObjectResult(new
            {
                error = new
                {
                    code = "capability_unavailable",
                    message = "Database diagnostics are not available for this environment.",
                    details = (object?)null
                }
            }) { StatusCode = StatusCodes.Status501NotImplemented };

        try
        {
            using var conn = _connectionFactory.CreateConnection(_connectionStrings.RequireForEnvironment(environment));
            return Ok(new { status = "Online", database = conn.Database });
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception)
        {
            return Ok(new { status = "Offline", error = "Database connectivity could not be verified." });
        }
    }
}
