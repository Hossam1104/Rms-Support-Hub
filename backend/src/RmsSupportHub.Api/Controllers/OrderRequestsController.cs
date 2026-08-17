using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using RmsSupportHub.Api.Exceptions;
using RmsSupportHub.Core;
using RmsSupportHub.Core.DTOs;
using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Services;
using RmsSupportHub.Data.Repositories;

namespace RmsSupportHub.Api.Controllers;

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
    private readonly IConnectionStringResolver _connectionStrings;
    private readonly IEnvironmentPolicy _environmentPolicy;

    public OrderRequestsController(
        IModuleRegistry moduleRegistry,
        IOrderRequestRepository repository,
        IApiClient apiClient,
        IConnectionStringResolver connectionStrings,
        IEnvironmentPolicy environmentPolicy)
    {
        _moduleRegistry = moduleRegistry;
        _repository = repository;
        _apiClient = apiClient;
        _connectionStrings = connectionStrings;
        _environmentPolicy = environmentPolicy;
    }

    [HttpGet]
    public async Task<ActionResult> List(
        string key,
        [FromQuery] OrderRequestListQuery query,
        [FromQuery] string? envKey = null,
        CancellationToken cancellationToken = default)
    {
        var (module, connStr, guard) = Resolve(key, envKey);
        if (guard != null) return guard;

        var page = query.Page is > 0 ? query.Page.Value : 1;
        var pageSize = Math.Clamp(query.PageSize ?? 25, 1, 200);

        var orderNumber = string.IsNullOrWhiteSpace(query.OrderNumber) ? query.Q : query.OrderNumber;
        string? normalizedPhone = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(query.Phone))
                normalizedPhone = Normalizers.NormalizePhoneSearch(query.Phone);
        }
        catch (ArgumentException)
        {
            return BadRequest(new { error = "Phone must contain at least 9 digits." });
        }

        if (query.DateFrom.HasValue && query.DateTo.HasValue && query.DateFrom.Value.Date > query.DateTo.Value.Date)
            return BadRequest(new { error = "dateFrom must be on or before dateTo." });

        var statuses = query.Statuses?.Distinct().ToArray();
        if (statuses is { Length: > 0 } && statuses.Any(status => status is < 1 or > 9))
            return BadRequest(new { error = "statuses must contain only values from 1 through 9." });
        if (query.Status is < 1 or > 9)
            return BadRequest(new { error = "status must be a value from 1 through 9." });

        var filters = new OrderRequestFilters(
            OrderNumber: string.IsNullOrWhiteSpace(orderNumber) ? null : orderNumber.Trim(),
            Phone: normalizedPhone,
            BranchCode: string.IsNullOrWhiteSpace(query.BranchCode) ? null : query.BranchCode.Trim(),
            Status: query.Status,
            Statuses: statuses,
            Succeeded: query.Succeeded,
            HasException: query.HasException,
            DateFrom: query.DateFrom,
            DateTo: query.DateTo,
            ExactOrderNumber: query.ExactMatch ?? true);

        // These reads share the same filter set but are independent. Run them
        // together so a slow count or aggregate cannot hold the page data
        // behind two other sequential database round trips.
        var itemsTask = _repository.ListAsync(connStr!, filters, page, pageSize, query.Sort, cancellationToken);
        var totalTask = _repository.CountAsync(connStr!, filters, cancellationToken);
        var statsTask = _repository.StatsAsync(connStr!, filters, cancellationToken);
        try
        {
            await Task.WhenAll(itemsTask, totalTask, statsTask);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Do not expose a SQL exception, connection string, or execution
            // plan detail to the operator. ExceptionMiddleware maps this to
            // the standard 502 envelope with a retryable message.
            throw new UpstreamException(
                "Order request data could not be loaded from the selected environment. Try again.");
        }

        var items = await itemsTask;
        var total = await totalTask;
        var stats = await statsTask;
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

        // A refresh can reduce the result set while the operator is on a later
        // page. Return the last real page instead of a misleading empty grid.
        if (totalPages > 0 && page > totalPages)
        {
            page = totalPages;
            try
            {
                items = await _repository.ListAsync(connStr!, filters, page, pageSize, query.Sort, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                throw new UpstreamException(
                    "Order request data could not be loaded from the selected environment. Try again.");
            }
        }

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

    /// <summary>Resolves cancellation to the registered environment's
    /// server-owned CancelUrl and re-checks CancelBlockedStatuses server-side.
    /// The client's disabled-button state is a UX convenience, not a security
    /// boundary.</summary>
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

        var env = CapabilityGuard.RequireEnvironment(_environmentPolicy, module!, envKey);
        var cancelUrl = env.CancelUrl;
        if (string.IsNullOrWhiteSpace(cancelUrl))
            throw new Exceptions.EnvironmentUnconfiguredException();

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

        JsonObject? payload;
        try
        {
            payload = JsonNode.Parse(detail.RequestJson) as JsonObject;
        }
        catch (JsonException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Stored request payload is not valid JSON." });
        }

        if (payload == null)
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Stored request payload must be a JSON object." });

        string? storedOrderNumber;
        try
        {
            storedOrderNumber = payload["order_code"]?.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { error = "Stored request order_code must be a string." });
        }
        if (string.IsNullOrWhiteSpace(storedOrderNumber))
            return BadRequest(new { error = "Stored request payload has no original order_code." });

        if (!string.Equals(storedOrderNumber.Trim(), detail.OrderNumber.Trim(), StringComparison.Ordinal))
        {
            return BadRequest(new { error = "Stored request order_code does not match the recorded order number." });
        }

        string? originalBranchCode;
        try
        {
            originalBranchCode = payload["branch_code"]?.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { error = "Stored request branch_code must be a string." });
        }
        var targetBranchCode = string.IsNullOrWhiteSpace(request.BranchCode)
            ? originalBranchCode?.Trim()
            : request.BranchCode.Trim();
        if (string.IsNullOrWhiteSpace(targetBranchCode))
            return BadRequest(new { error = "No original or target branch code is available for this request." });

        // JsonObject is an in-memory copy parsed from the immutable stored
        // RequestJson string. Only the verified branch override is changed;
        // order_code and every unknown payload field remain untouched.
        payload["branch_code"] = targetBranchCode;

        var env = CapabilityGuard.RequireEnvironment(_environmentPolicy, module!, envKey);
        var url = env.ApiUrl;
        if (string.IsNullOrWhiteSpace(url))
            throw new Exceptions.EnvironmentUnconfiguredException();

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

        var env = CapabilityGuard.RequireEnvironment(_environmentPolicy, module, envKey);
        var connStr = _connectionStrings.RequireForEnvironment(env);

        return (module, connStr, null);
    }
}
