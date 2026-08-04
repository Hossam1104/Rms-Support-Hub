using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OnlineOrderTool.Api;
using OnlineOrderTool.Api.Controllers;
using OnlineOrderTool.Core.DTOs;
using OnlineOrderTool.Core.Modules;
using OnlineOrderTool.Core.Services;
using OnlineOrderTool.Data;
using OnlineOrderTool.Data.Repositories;
using Xunit;

namespace OnlineOrderTool.Tests;

/// <summary>Captures what OrderRequestsController.Cancel actually posts to,
/// proving the historical bug (cancel silently posting to the create-order
/// URL because the endpoint picker's field name was never wired up -- see
/// remediation_plan.md B12) cannot recur: the URL sent must contain
/// "CancelOrder" and must never contain "CreateAndAssignOrder".</summary>
public class OrderRequestsControllerTests
{
    private static IModuleRegistry BuildRegistry() => new ModuleRegistry(
        new FlatOrderPayloadBuilder(),
        new FlatOrderValidator(),
        new UniCommercePayloadBuilder(),
        new UniCommerceValidator(),
        new FlatOrderItemRepository(new SqlServerConnectionFactory()),
        new GhcConsumerRepository(new SqlServerConnectionFactory()),
        new UpcItemRepository(new SqlServerConnectionFactory()),
        new UpcConsumerRepository(new SqlServerConnectionFactory()));

    private static IConfiguration BuildConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:UpcEcommerceTest"] = "Server=fake;Database=fake;"
        })
        .Build();

    private static OrderRequestDetailDto MakeDetail(int orderStatus) => new(
        Id: 42,
        OrderNumber: "UPC-1",
        OrderDate: DateTime.UtcNow,
        NetTotal: 100m,
        ItemCount: 1,
        IsSucceeded: true,
        ExceptionMessage: null,
        RequestJson: "{\"order_code\":\"UPC-1\",\"branch_code\":\"100\",\"unknown_field\":{\"keep\":true}}",
        ResponseJson: null,
        Header: new OrderRequestHeaderDto(
            OrderHeaderId: 1,
            OrderNumber: "UPC-1",
            BranchCode: "100",
            BranchName: "Main",
            OrderStatus: orderStatus,
            OrderStatusLabel: OnlineOrderTool.Core.OrderRequestStatus.GetLabel(orderStatus),
            OrderDate: DateTime.UtcNow,
            ConsumerMobile: "0500000000",
            Address: "Some address",
            GrossTotal: 100m,
            NetTotal: 100m,
            TotalVat: 0m,
            TotalDiscount: 0m,
            OrderPaymentMethod: "COD",
            OrderNote: null,
            ParentOrderNumber: null,
            RejectionMessage: null,
            CanResend: OnlineOrderTool.Core.OrderRequestStatus.IsResendAllowed(orderStatus),
            CanCancel: true),
        Details: new List<OrderRequestDetailLineDto>(),
        Transactions: new List<OrderRequestTransactionDto>(),
        Invoice: null);

    private class FakeOrderRequestRepository : IOrderRequestRepository
    {
        public OrderRequestDetailDto Detail = MakeDetail(orderStatus: 1); // 1 = New, cancel-allowed
        public OrderRequestFilters? LastFilters;
        public CancellationToken LastCancellationToken;
        public int LastPage;
        public int LastPageSize;
        public int Total = 0;
        public Exception? ListException;

        public Task<List<OrderRequestListItemDto>> ListAsync(
            string connectionString, OrderRequestFilters filters, int page, int pageSize, string? sort,
            CancellationToken cancellationToken = default)
        {
            LastFilters = filters;
            LastCancellationToken = cancellationToken;
            LastPage = page;
            LastPageSize = pageSize;
            return ListException == null
                ? Task.FromResult(new List<OrderRequestListItemDto>())
                : Task.FromException<List<OrderRequestListItemDto>>(ListException);
        }

        public Task<int> CountAsync(string connectionString, OrderRequestFilters filters, CancellationToken cancellationToken = default)
            => Task.FromResult(Total);

        public Task<OrderRequestStatsDto> StatsAsync(string connectionString, OrderRequestFilters filters, CancellationToken cancellationToken = default)
            => Task.FromResult(new OrderRequestStatsDto(Total, 0, 0, 0));

        public Task<OrderRequestDetailDto?> GetDetailAsync(string connectionString, long requestId)
            => Task.FromResult<OrderRequestDetailDto?>(Detail);

        public Task<List<OrderRequestAttemptDto>> ListAttemptsAsync(string connectionString, string orderNumber)
            => Task.FromResult(new List<OrderRequestAttemptDto>());

        public Task<OrderRequestLineageDto> GetLineageAsync(string connectionString, string orderNumber, string? parentOrderNumber)
            => Task.FromResult(new OrderRequestLineageDto(null, new List<OrderRequestLineageNodeDto>()));
    }

    private class FakeApiClient : IApiClient
    {
        public string? LastUrl;
        public string? LastPayloadJson;
        public int CallCount;

        public Task<ApiResponseResult> SendOrderAsync(string url, object payloadJson)
        {
            LastUrl = url;
            LastPayloadJson = JsonSerializer.Serialize(payloadJson);
            CallCount++;
            return Task.FromResult(new ApiResponseResult(200, "{\"ok\":true}", url, true));
        }

        public Task<bool> TestEndpointAsync(string url) => Task.FromResult(true);
    }

    [Fact]
    public async Task Cancel_PostsToCancelUrl_NeverToCreateAndAssignOrderUrl()
    {
        var repo = new FakeOrderRequestRepository();
        var apiClient = new FakeApiClient();
        var controller = new OrderRequestsController(BuildRegistry(), repo, apiClient, BuildConfiguration());

        var result = await controller.Cancel(
            "upc_ecommerce", 42,
            new OrderRequestCancelRequest("Customer request", null, null),
            envKey: "UPC Testing");

        Assert.NotNull(apiClient.LastUrl);
        Assert.Contains("CancelOrder", apiClient.LastUrl);
        Assert.DoesNotContain("CreateAndAssignOrder", apiClient.LastUrl);
    }

    [Fact]
    public async Task Cancel_OnAlreadyDoneOrder_Returns409WithoutCallingApi()
    {
        var repo = new FakeOrderRequestRepository { Detail = MakeDetail(orderStatus: 9) }; // 9 = Done, cancel-blocked
        var apiClient = new FakeApiClient();
        var controller = new OrderRequestsController(BuildRegistry(), repo, apiClient, BuildConfiguration());

        var result = await controller.Cancel(
            "upc_ecommerce", 42,
            new OrderRequestCancelRequest("Customer request", null, null),
            envKey: "UPC Testing");

        var objectResult = Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
        Assert.Null(apiClient.LastUrl);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task Resend_BlocksNewAndWithDelegateBeforeCallingApi(int status)
    {
        var repo = new FakeOrderRequestRepository { Detail = MakeDetail(status) };
        var apiClient = new FakeApiClient();
        var controller = new OrderRequestsController(BuildRegistry(), repo, apiClient, BuildConfiguration());

        var result = await controller.Resend(
            "upc_ecommerce", 42,
            new OrderRequestResendRequest("200", null),
            envKey: "UPC Testing");

        var conflict = Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal(0, apiClient.CallCount);
    }

    [Fact]
    public async Task Resend_ReusesStoredNumberAndUnknownFields_WhileChangingOnlyBranch()
    {
        var repo = new FakeOrderRequestRepository { Detail = MakeDetail(orderStatus: 2) };
        var originalJson = repo.Detail.RequestJson;
        var apiClient = new FakeApiClient();
        var controller = new OrderRequestsController(BuildRegistry(), repo, apiClient, BuildConfiguration());

        var result = await controller.Resend(
            "upc_ecommerce", 42,
            new OrderRequestResendRequest("200", null),
            envKey: "UPC Testing");

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        Assert.Equal(1, apiClient.CallCount);
        Assert.Equal(originalJson, repo.Detail.RequestJson);
        using var sent = JsonDocument.Parse(apiClient.LastPayloadJson!);
        Assert.Equal("UPC-1", sent.RootElement.GetProperty("order_code").GetString());
        Assert.Equal("200", sent.RootElement.GetProperty("branch_code").GetString());
        Assert.True(sent.RootElement.GetProperty("unknown_field").GetProperty("keep").GetBoolean());
        Assert.Contains("CreateAndAssignOrder", apiClient.LastUrl);
    }

    [Fact]
    public async Task Resend_UsesOriginalBranchWhenNoOverrideIsProvided()
    {
        var repo = new FakeOrderRequestRepository { Detail = MakeDetail(orderStatus: 2) };
        var apiClient = new FakeApiClient();
        var controller = new OrderRequestsController(BuildRegistry(), repo, apiClient, BuildConfiguration());

        await controller.Resend(
            "upc_ecommerce", 42,
            new OrderRequestResendRequest(null, null),
            envKey: "UPC Testing");

        using var sent = JsonDocument.Parse(apiClient.LastPayloadJson!);
        Assert.Equal("100", sent.RootElement.GetProperty("branch_code").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-json")]
    [InlineData("{\"branch_code\":\"100\"}")]
    [InlineData("{\"order_code\":\"OTHER\",\"branch_code\":\"100\"}")]
    public async Task Resend_FailsSafelyWhenStoredPayloadCannotProveOriginalNumber(string? requestJson)
    {
        var repo = new FakeOrderRequestRepository { Detail = MakeDetail(orderStatus: 2) with { RequestJson = requestJson } };
        var apiClient = new FakeApiClient();
        var controller = new OrderRequestsController(BuildRegistry(), repo, apiClient, BuildConfiguration());

        var result = await controller.Resend(
            "upc_ecommerce", 42,
            new OrderRequestResendRequest("200", null),
            envKey: "UPC Testing");

        var objectResult = Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ObjectResult>(result);
        Assert.NotEqual(200, objectResult.StatusCode);
        Assert.Equal(0, apiClient.CallCount);
    }

    private static OrderRequestListQuery ListQuery(
        string? phone = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        bool? exactMatch = null,
        int? status = null,
        int page = 2,
        int pageSize = 50)
        => new(
            Q: null,
            OrderNumber: "  UPC-%  ",
            Phone: phone,
            BranchCode: " P001 ",
            Status: status,
            Statuses: new[] { 6, 7 },
            Succeeded: false,
            HasException: null,
            DateFrom: dateFrom,
            DateTo: dateTo,
            Page: page,
            PageSize: pageSize,
            Sort: "-net_total",
            ExactMatch: exactMatch);

    [Fact]
    public async Task List_NormalizesPhoneAndPassesOneCanonicalFilterSetToAllReads()
    {
        var repo = new FakeOrderRequestRepository();
        var controller = new OrderRequestsController(BuildRegistry(), repo, new FakeApiClient(), BuildConfiguration());
        using var cancellation = new CancellationTokenSource();

        var result = await controller.List(
            "upc_ecommerce", ListQuery(phone: "+966556028080", exactMatch: false), "UPC Testing", cancellation.Token);

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        Assert.NotNull(repo.LastFilters);
        Assert.Equal("556028080", repo.LastFilters!.Phone);
        Assert.False(repo.LastFilters.ExactOrderNumber);
        Assert.Equal("P001", repo.LastFilters.BranchCode);
        Assert.Equal(new[] { 6, 7 }, repo.LastFilters.Statuses);
        Assert.Equal(2, repo.LastPage);
        Assert.Equal(cancellation.Token, repo.LastCancellationToken);
    }

    [Fact]
    public async Task List_RejectsInvalidDateRangeBeforeStartingDatabaseReads()
    {
        var repo = new FakeOrderRequestRepository();
        var controller = new OrderRequestsController(BuildRegistry(), repo, new FakeApiClient(), BuildConfiguration());

        var result = await controller.List(
            "upc_ecommerce", ListQuery(dateFrom: new DateTime(2026, 8, 4), dateTo: new DateTime(2026, 8, 3)), "UPC Testing");

        var badRequest = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.Null(repo.LastFilters);
    }

    [Fact]
    public async Task List_RejectsInvalidPhoneBeforeStartingDatabaseReads()
    {
        var repo = new FakeOrderRequestRepository();
        var controller = new OrderRequestsController(BuildRegistry(), repo, new FakeApiClient(), BuildConfiguration());

        var result = await controller.List(
            "upc_ecommerce", ListQuery(phone: "123"), "UPC Testing");

        var badRequest = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.Null(repo.LastFilters);
    }

    [Fact]
    public async Task List_RejectsInvalidStatusBeforeStartingDatabaseReads()
    {
        var repo = new FakeOrderRequestRepository();
        var controller = new OrderRequestsController(BuildRegistry(), repo, new FakeApiClient(), BuildConfiguration());

        var result = await controller.List(
            "upc_ecommerce", ListQuery(status: 99), "UPC Testing");

        var badRequest = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.Null(repo.LastFilters);
    }

    [Fact]
    public async Task List_ClampsInvalidPageAndPageSizeBeforeRepositoryPaging()
    {
        var repo = new FakeOrderRequestRepository();
        var controller = new OrderRequestsController(BuildRegistry(), repo, new FakeApiClient(), BuildConfiguration());

        var result = await controller.List(
            "upc_ecommerce", ListQuery(page: 0, pageSize: 999), "UPC Testing");

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        Assert.Equal(1, repo.LastPage);
        Assert.Equal(200, repo.LastPageSize);
    }

    [Fact]
    public async Task List_FallsBackToLastRealPageWhenTotalShrinks()
    {
        var repo = new FakeOrderRequestRepository { Total = 1 };
        var controller = new OrderRequestsController(BuildRegistry(), repo, new FakeApiClient(), BuildConfiguration());

        var result = await controller.List(
            "upc_ecommerce", ListQuery(page: 2, pageSize: 50), "UPC Testing");

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        Assert.Equal(1, repo.LastPage);
    }

    [Fact]
    public async Task List_ConvertsRepositoryFailureToRetryableUpstreamError()
    {
        var repo = new FakeOrderRequestRepository { ListException = new InvalidOperationException("secret SQL details") };
        var controller = new OrderRequestsController(BuildRegistry(), repo, new FakeApiClient(), BuildConfiguration());

        var error = await Assert.ThrowsAsync<OnlineOrderTool.Api.Exceptions.UpstreamException>(() =>
            controller.List("upc_ecommerce", ListQuery(), "UPC Testing"));

        Assert.Equal(502, error.StatusCode);
        Assert.DoesNotContain("secret SQL details", error.Message);
    }
}
