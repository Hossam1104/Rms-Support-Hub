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
        RequestJson: "{\"branch_code\":\"100\"}",
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
            CanResend: true,
            CanCancel: true),
        Details: new List<OrderRequestDetailLineDto>(),
        Transactions: new List<OrderRequestTransactionDto>(),
        Invoice: null);

    private class FakeOrderRequestRepository : IOrderRequestRepository
    {
        public OrderRequestDetailDto Detail = MakeDetail(orderStatus: 1); // 1 = New, cancel-allowed

        public Task<List<OrderRequestListItemDto>> ListAsync(string connectionString, OrderRequestFilters filters, int page, int pageSize, string? sort)
            => Task.FromResult(new List<OrderRequestListItemDto>());

        public Task<int> CountAsync(string connectionString, OrderRequestFilters filters) => Task.FromResult(0);

        public Task<OrderRequestStatsDto> StatsAsync(string connectionString, OrderRequestFilters filters)
            => Task.FromResult(new OrderRequestStatsDto(0, 0, 0, 0));

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

        public Task<ApiResponseResult> SendOrderAsync(string url, object payloadJson)
        {
            LastUrl = url;
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
}
