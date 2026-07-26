using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OnlineOrderTool.Core;
using OnlineOrderTool.Core.DTOs;
using OnlineOrderTool.Core.Modules;
using OnlineOrderTool.Core.Services;
using OnlineOrderTool.Data.Repositories;

namespace OnlineOrderTool.Api.Controllers;

/// <summary>Reads and acts on the real OrderRequests table (via R4's
/// OrderRequestRepository) -- the source of truth for what this tool sent
/// and what came back, replacing the deleted order-history JSON files and
/// the deleted UPC-only ValidationController/UpcOrderValidationRepository
/// (which read RequestOrderHeaders-first and never surfaced
/// ResponseJson/ExceptionMessage; see remediation_plan.md B6-B11).</summary>
[ApiController]
[Route("api/modules/{key}/order-requests")]
public class OrderRequestsController : ControllerBase
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IOrderRequestRepository _repository;
    private readonly IApiClient _apiClient;
    private readonly IConfiguration _configuration;

    public OrderRequestsController(
        IModuleRegistry moduleRegistry,
        IOrderRequestRepository repository,
        IApiClient apiClient,
        IConfiguration configuration)
    {
        _moduleRegistry = moduleRegistry;
        _repository = repository;
        _apiClient = apiClient;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult> List(string key, [FromQuery] OrderRequestListQuery query, [FromQuery] string? envKey = null)
    {
        var (module, connStr, guard) = Resolve(key, envKey);
        if (guard != null) return guard;

        var page = query.Page is > 0 ? query.Page.Value : 1;
        var pageSize = Math.Clamp(query.PageSize ?? 25, 1, 200);
        var filters = new OrderRequestFilters(
            OrderNumber: string.IsNullOrWhiteSpace(query.OrderNumber) ? query.Q : query.OrderNumber,
            Phone: query.Phone,
            BranchCode: query.BranchCode,
            Status: query.Status,
            Statuses: query.Statuses,
            Succeeded: query.Succeeded,
            HasException: query.HasException,
            DateFrom: query.DateFrom,
            DateTo: query.DateTo);

        var items = await _repository.ListAsync(connStr!, filters, page, pageSize, query.Sort);
        var total = await _repository.CountAsync(connStr!, filters);
        var stats = await _repository.StatsAsync(connStr!, filters);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

        return Ok(new { items, page, pageSize, total, totalPages, stats });
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult> GetDetail(string key, long id, [FromQuery] string? envKey = null)
    {
        var (module, connStr, guard) = Resolve(key, envKey);
        if (guard != null) return guard;

        var detail = await _repository.GetDetailAsync(connStr!, id);
        if (detail == null) return NotFound(new { error = $"Order request '{id}' not found." });

        var attempts = await _repository.ListAttemptsAsync(connStr!, detail.OrderNumber);
        var lineage = await _repository.GetLineageAsync(connStr!, detail.OrderNumber, detail.Header?.ParentOrderNumber);

        return Ok(new { request = detail, attempts, lineage });
    }

    [HttpGet("by-order/{orderNumber}")]
    public async Task<ActionResult> GetByOrderNumber(string key, string orderNumber, [FromQuery] string? envKey = null)
    {
        var (module, connStr, guard) = Resolve(key, envKey);
        if (guard != null) return guard;

        var attempts = await _repository.ListAttemptsAsync(connStr!, orderNumber);
        if (attempts.Count == 0) return NotFound(new { error = $"No requests found for order '{orderNumber}'." });

        var latestId = attempts.Max(a => a.Id);
        var detail = await _repository.GetDetailAsync(connStr!, latestId);
        var lineage = await _repository.GetLineageAsync(connStr!, orderNumber, detail?.Header?.ParentOrderNumber);

        return Ok(new { request = detail, attempts, lineage });
    }

    [HttpGet("branches")]
    public async Task<ActionResult> GetBranches(string key, [FromQuery] string? envKey = null)
    {
        var (module, connStr, guard) = Resolve(key, envKey);
        if (guard != null) return guard;

        var branches = await _repository.ListBranchesAsync(connStr!);
        return Ok(branches);
    }

    /// <summary>Resolves the URL as customUrl -> endpointKey -> environment.CancelUrl
    /// (never ApiUrl -- that bug lived in the now-deleted HistoryController,
    /// see remediation_plan.md B12) and re-checks CancelBlockedStatuses
    /// server-side: the client's disabled-button state is a UX convenience,
    /// not a security boundary.</summary>
    [HttpPost("{id:long}/cancel")]
    public async Task<ActionResult> Cancel(string key, long id, [FromBody] OrderRequestCancelRequest request, [FromQuery] string? envKey = null)
    {
        var (module, connStr, guard) = Resolve(key, envKey, requireCancel: true);
        if (guard != null) return guard;

        var detail = await _repository.GetDetailAsync(connStr!, id);
        if (detail == null) return NotFound(new { error = $"Order request '{id}' not found." });
        if (detail.Header == null)
            return BadRequest(new { error = "This request has no order header; there is nothing to cancel." });

        if (!OrderRequestStatus.IsCancelAllowed(detail.Header.OrderStatus))
        {
            var reason = $"Order status '{detail.Header.OrderStatusLabel}' cannot be cancelled.";
            return StatusCode(StatusCodes.Status409Conflict, new { error = reason, cancelBlockedReason = reason });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "A cancellation reason is required." });

        var env = module!.GetEnvironment(envKey);
        var cancelUrl = !string.IsNullOrWhiteSpace(request.CustomUrl) ? request.CustomUrl : env.CancelUrl;
        if (string.IsNullOrWhiteSpace(cancelUrl))
            return BadRequest(new { error = $"No cancel URL configured for environment '{env.Key}'." });

        var cancelPayload = new { order_number = detail.OrderNumber, cancel_reason = request.Reason };
        var apiResult = await _apiClient.SendOrderAsync(cancelUrl, cancelPayload);

        var refreshed = await _repository.GetDetailAsync(connStr!, id);

        return Ok(new
        {
            success = apiResult.Success,
            statusCode = apiResult.StatusCode,
            responseText = apiResult.ResponseText,
            urlSent = apiResult.UrlSent,
            request = refreshed
        });
    }

    /// <summary>Rebuilds the payload from THIS order's own stored RequestJson
    /// and overrides only branch_code -- never the live draft (see
    /// remediation_plan.md B13; OrderController.ResendOrder, which mutated
    /// and persisted the live draft's branch_code before resending it, has
    /// been deleted -- this endpoint is its replacement).</summary>
    [HttpPost("{id:long}/resend")]
    public async Task<ActionResult> Resend(string key, long id, [FromBody] OrderRequestResendRequest request, [FromQuery] string? envKey = null)
    {
        var (module, connStr, guard) = Resolve(key, envKey, requireResend: true);
        if (guard != null) return guard;

        if (string.IsNullOrWhiteSpace(request.BranchCode))
            return BadRequest(new { error = "branchCode is required." });

        var detail = await _repository.GetDetailAsync(connStr!, id);
        if (detail == null) return NotFound(new { error = $"Order request '{id}' not found." });
        if (detail.Header == null)
            return BadRequest(new { error = "This request has no order header; there is nothing to resend." });

        if (!OrderRequestStatus.IsResendAllowed(detail.Header.OrderStatus))
        {
            var reason = $"Order status '{detail.Header.OrderStatusLabel}' cannot be resent to another branch.";
            return StatusCode(StatusCodes.Status409Conflict, new { error = reason, resendBlockedReason = reason });
        }

        if (string.IsNullOrWhiteSpace(detail.RequestJson))
            return BadRequest(new { error = "Original request payload not found for this order." });

        Dictionary<string, object?>? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(detail.RequestJson);
        }
        catch (JsonException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = $"Stored request payload is not valid JSON: {ex.Message}" });
        }

        if (payload == null)
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Stored request payload is empty." });

        payload["branch_code"] = request.BranchCode;

        var env = module!.GetEnvironment(envKey);
        var url = env.ApiUrl;
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new { error = $"No API endpoint configured for environment '{env.Key}'." });

        var apiResult = await _apiClient.SendOrderAsync(url, payload);

        return Ok(new
        {
            success = apiResult.Success,
            statusCode = apiResult.StatusCode,
            responseText = apiResult.ResponseText,
            urlSent = apiResult.UrlSent
        });
    }

    private (IOrderModule? Module, string? ConnectionString, ObjectResult? Guard) Resolve(
        string key, string? envKey, bool requireCancel = false, bool requireResend = false)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null)
        {
            return (null, null, new ObjectResult(new { error = $"Unknown module '{key}'" }) { StatusCode = StatusCodes.Status404NotFound });
        }

        var guard = CapabilityGuard.Require(module, c => c.OrderRequests, "order-requests");
        if (guard != null) return (module, null, guard);

        if (requireCancel)
        {
            guard = CapabilityGuard.Require(module, c => c.Cancel, "cancel");
            if (guard != null) return (module, null, guard);
        }

        if (requireResend)
        {
            guard = CapabilityGuard.Require(module, c => c.Resend, "resend");
            if (guard != null) return (module, null, guard);
        }

        var env = module.GetEnvironment(envKey);
        var name = env.ConnectionStringName
            ?? throw new InvalidOperationException($"Environment '{env.Key}' has no ConnectionStringName configured.");
        var connStr = ConnectionStringResolver.Require(_configuration, name);
        if (!connStr.Contains("Connect Timeout", StringComparison.OrdinalIgnoreCase))
        {
            connStr += ";Connect Timeout=5;";
        }

        return (module, connStr, null);
    }
}
