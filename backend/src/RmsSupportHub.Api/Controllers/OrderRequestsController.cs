using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Mvc;
using RmsSupportHub.Api.Configuration;
using RmsSupportHub.Api.Exceptions;
using RmsSupportHub.Core;
using RmsSupportHub.Core.DTOs;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Services;
using RmsSupportHub.Api.Middleware;
using RmsSupportHub.Api.Security;
using RmsSupportHub.Data.Repositories;

namespace RmsSupportHub.Api.Controllers;

/// <summary>Reads module-selected upstream request-history tables (the
/// standard OrderRequests workflow or a verified bounded adapter) -- the
/// source of truth for what this tool sent and what came back, replacing the
/// deleted order-history JSON files and
/// the deleted UPC-only ValidationController/UpcOrderValidationRepository
/// (which read RequestOrderHeaders-first and never surfaced
/// ResponseJson/ExceptionMessage; see remediation_plan.md B6-B11).</summary>
[ApiController]
[Route("api/modules/{key}/order-requests")]
public class OrderRequestsController : ControllerBase
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IOrderRequestRepository _repository;
    private readonly IGhcUnicommerceOrderRequestRepository? _ghcUnicommerceRepository;
    private readonly IApiClient _apiClient;
    private readonly IConnectionStringResolver _connectionStrings;
    private readonly IEnvironmentPolicy _environmentPolicy;
    private readonly IProductionMutationGate _productionGate;
    private readonly IOutboundApiKeyResolver _apiKeys;

    public OrderRequestsController(
        IModuleRegistry moduleRegistry,
        IOrderRequestRepository repository,
        IApiClient apiClient,
        IConnectionStringResolver connectionStrings,
        IEnvironmentPolicy environmentPolicy,
        IGhcUnicommerceOrderRequestRepository? ghcUnicommerceRepository = null,
        IProductionMutationGate? productionGate = null,
        IOutboundApiKeyResolver? apiKeys = null)
    {
        _moduleRegistry = moduleRegistry;
        _repository = repository;
        _ghcUnicommerceRepository = ghcUnicommerceRepository;
        _apiClient = apiClient;
        _connectionStrings = connectionStrings;
        _environmentPolicy = environmentPolicy;
        _productionGate = productionGate
            ?? new ProductionMutationGate(null, NullLogger<ProductionMutationGate>.Instance);
        _apiKeys = apiKeys ?? new EmptyOutboundApiKeyResolver();
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

        var filterGuard = ValidateHistoryQuery(module!, query);
        if (filterGuard != null) return filterGuard;

        var repository = RepositoryFor(module!);

        // These reads share the same filter set but are independent. Run them
        // together so a slow count or aggregate cannot hold the page data
        // behind two other sequential database round trips.
        var itemsTask = repository.ListAsync(connStr!, filters, page, pageSize, query.Sort, cancellationToken);
        var totalTask = repository.CountAsync(connStr!, filters, cancellationToken);
        var statsTask = repository.StatsAsync(connStr!, filters, cancellationToken);
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
                items = await repository.ListAsync(connStr!, filters, page, pageSize, query.Sort, cancellationToken);
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

        var repository = RepositoryFor(module!);
        var detail = await repository.GetDetailAsync(connStr!, id);
        if (detail == null) return NotFound(new { error = $"Order request '{id}' not found." });

        var attempts = await repository.ListAttemptsAsync(connStr!, detail.OrderNumber);
        var lineage = await repository.GetLineageAsync(connStr!, detail.OrderNumber, detail.Header?.ParentOrderNumber);

        return Ok(new { request = detail, attempts, lineage });
    }

    [HttpGet("by-order/{orderNumber}")]
    public async Task<ActionResult> GetByOrderNumber(string key, string orderNumber, [FromQuery] string? envKey = null)
    {
        var (module, connStr, guard) = Resolve(key, envKey);
        if (guard != null) return guard;

        var repository = RepositoryFor(module!);
        var attempts = await repository.ListAttemptsAsync(connStr!, orderNumber);
        if (attempts.Count == 0) return NotFound(new { error = $"No requests found for order '{orderNumber}'." });

        var latestId = attempts.Max(a => a.Id);
        var detail = await repository.GetDetailAsync(connStr!, latestId);
        var lineage = await repository.GetLineageAsync(connStr!, orderNumber, detail?.Header?.ParentOrderNumber);

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

        var env = CapabilityGuard.RequireEnvironment(_environmentPolicy, module!, envKey);
        _productionGate.RequireUnlocked(
            module!,
            env,
            ProductionToken(),
            SessionIdForGate(),
            "cancel");

        var repository = RepositoryFor(module!);
        var detail = await repository.GetDetailAsync(connStr!, id);
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

        var cancelUrl = env.CancelUrl;
        if (string.IsNullOrWhiteSpace(cancelUrl))
            throw new Exceptions.EnvironmentUnconfiguredException();

        var cancelPayload = new { order_number = detail.OrderNumber, cancel_reason = request.Reason };
        var apiResult = await SendDownstreamAsync(env, cancelPayload, cancelUrl);

        var refreshed = await repository.GetDetailAsync(connStr!, id);

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

        var env = CapabilityGuard.RequireEnvironment(_environmentPolicy, module!, envKey);
        _productionGate.RequireUnlocked(
            module!,
            env,
            ProductionToken(),
            SessionIdForGate(),
            "resend");

        var repository = RepositoryFor(module!);
        var detail = await repository.GetDetailAsync(connStr!, id);
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

        var url = env.ApiUrl;
        if (string.IsNullOrWhiteSpace(url))
            throw new Exceptions.EnvironmentUnconfiguredException();

        var apiResult = await SendDownstreamAsync(env, payload, url);

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

    private IOrderRequestRepository RepositoryFor(IOrderModule module)
    {
        if (module.Capabilities.OrderRequestHistory == OrderRequestHistoryMode.ExternalInvoiceRequests)
        {
            return _ghcUnicommerceRepository
                ?? throw new InvalidOperationException(
                    "The configured module history adapter is not registered.");
        }

        return _repository;
    }

    private static ObjectResult? ValidateHistoryQuery(IOrderModule module, OrderRequestListQuery query)
    {
        if (module.Capabilities.OrderRequestHistory != OrderRequestHistoryMode.ExternalInvoiceRequests)
            return null;

        if (!string.IsNullOrWhiteSpace(query.Phone)
            || !string.IsNullOrWhiteSpace(query.BranchCode)
            || query.Status.HasValue
            || query.Statuses is { Length: > 0 }
            || query.HasException.HasValue)
        {
            return new BadRequestObjectResult(new
            {
                error = "Uni-Commerce history supports order/reference, outcome, and date filters only; exception state is unavailable."
            });
        }

        if (!string.IsNullOrWhiteSpace(query.Sort)
            && !string.Equals(query.Sort.TrimStart('+', '-'), "order_date", StringComparison.OrdinalIgnoreCase))
        {
            return new BadRequestObjectResult(new
            {
                error = "Uni-Commerce history supports sorting by order date only."
            });
        }

        return null;
    }

    private async Task<ApiResponseResult> SendDownstreamAsync(
        ModuleEnvironment environment,
        object payload,
        string? urlOverride = null)
    {
        var url = urlOverride ?? environment.ApiUrl;
        if (string.IsNullOrWhiteSpace(url))
            throw new EnvironmentUnconfiguredException();

        var apiKey = _apiKeys.Resolve(environment);
        if (environment.RequiresApiKey && string.IsNullOrWhiteSpace(apiKey))
            throw new EnvironmentUnconfiguredException();

        return string.IsNullOrWhiteSpace(apiKey)
            ? await _apiClient.SendOrderAsync(url, payload)
            : await _apiClient.SendOrderWithApiKeyAsync(url, payload, apiKey);
    }

    private string SessionIdForGate() =>
        HttpContext?.Items[SessionIdMiddleware.CookieName] as string ?? "direct-controller-session";

    private string? ProductionToken() =>
        ControllerContext?.HttpContext?.Request.Headers[ProductionMutationGate.UnlockHeaderName].FirstOrDefault();
}
