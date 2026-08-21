using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RmsSupportHub.Api;
using RmsSupportHub.Api.Controllers;
using RmsSupportHub.Api.Security;
using RmsSupportHub.Core.DTOs;
using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Services;
using RmsSupportHub.Data;
using RmsSupportHub.Data.Repositories;
using Xunit;

namespace RmsSupportHub.Tests;

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
        new GhcUnicommerceConsumerRepository(new SqlServerConnectionFactory()),
        new UpcItemRepository(new SqlServerConnectionFactory()),
        new UpcConsumerRepository(new SqlServerConnectionFactory()),
        TestEnvironmentCatalog.UpcAndGhcUni());

    private static IConfiguration BuildConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:UpcEcommerceTest"] = "Server=fake;Database=fake;",
            ["ConnectionStrings:GhcUnicommerceTest"] = "Server=fake;Database=fake;"
        })
        .Build();

    private static OrderRequestsController BuildController(
        IOrderRequestRepository repository,
        IApiClient apiClient,
        DeploymentTier deploymentTier = DeploymentTier.Testing,
        IGhcUnicommerceOrderRequestRepository? ghcUnicommerceRepository = null,
        IProductionMutationGate? productionGate = null) =>
        new(
            BuildRegistry(),
            repository,
            apiClient,
            new RmsSupportHub.Api.ServerConnectionStringResolver(BuildConfiguration()),
            new EnvironmentPolicy(deploymentTier),
            ghcUnicommerceRepository,
            productionGate);

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
            OrderStatusLabel: RmsSupportHub.Core.OrderRequestStatus.GetLabel(orderStatus),
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
            CanResend: RmsSupportHub.Core.OrderRequestStatus.IsResendAllowed(orderStatus),
            CanCancel: true),
        Details: new List<OrderRequestDetailLineDto>(),
        Transactions: new List<OrderRequestTransactionDto>(),
        Invoice: null);

    private class FakeOrderRequestRepository : IOrderRequestRepository
    {
        public OrderRequestDetailDto Detail = MakeDetail(orderStatus: 1); // 1 = New, cancel-allowed
        public ConcurrentBag<string> ConnectionStrings { get; } = new();
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
            ConnectionStrings.Add(connectionString);
            LastFilters = filters;
            LastCancellationToken = cancellationToken;
            LastPage = page;
            LastPageSize = pageSize;
            return ListException == null
                ? Task.FromResult(new List<OrderRequestListItemDto>())
                : Task.FromException<List<OrderRequestListItemDto>>(ListException);
        }

        public Task<int> CountAsync(string connectionString, OrderRequestFilters filters, CancellationToken cancellationToken = default)
        {
            ConnectionStrings.Add(connectionString);
            return Task.FromResult(Total);
        }

        public Task<OrderRequestStatsDto> StatsAsync(string connectionString, OrderRequestFilters filters, CancellationToken cancellationToken = default)
        {
            ConnectionStrings.Add(connectionString);
            return Task.FromResult(new OrderRequestStatsDto(Total, 0, 0, 0));
        }

        public Task<OrderRequestDetailDto?> GetDetailAsync(string connectionString, long requestId)
        {
            ConnectionStrings.Add(connectionString);
            return Task.FromResult<OrderRequestDetailDto?>(Detail);
        }

        public Task<List<OrderRequestAttemptDto>> ListAttemptsAsync(string connectionString, string orderNumber)
        {
            ConnectionStrings.Add(connectionString);
            return Task.FromResult(new List<OrderRequestAttemptDto>());
        }

        public Task<OrderRequestLineageDto> GetLineageAsync(string connectionString, string orderNumber, string? parentOrderNumber)
        {
            ConnectionStrings.Add(connectionString);
            return Task.FromResult(new OrderRequestLineageDto(null, new List<OrderRequestLineageNodeDto>()));
        }
    }

    private sealed class FakeGhcUnicommerceOrderRequestRepository : FakeOrderRequestRepository,
        IGhcUnicommerceOrderRequestRepository
    {
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

        public Task<ApiResponseResult> SendOrderWithApiKeyAsync(string url, object payloadJson, string apiKey) =>
            string.IsNullOrWhiteSpace(apiKey)
                ? Task.FromResult(new ApiResponseResult(500, "", url, false))
                : SendOrderAsync(url, payloadJson);

        public Task<bool> TestEndpointAsync(string url, TimeSpan? timeout = null) => Task.FromResult(true);
    }

    [Fact]
    public async Task Cancel_PostsToCancelUrl_NeverToCreateAndAssignOrderUrl()
    {
        var repo = new FakeOrderRequestRepository();
        var apiClient = new FakeApiClient();
        var controller = BuildController(repo, apiClient);

        var result = await controller.Cancel(
            "upc_ecommerce", 42,
            new OrderRequestCancelRequest("Customer request"),
            envKey: "UPC Testing");

        Assert.NotNull(apiClient.LastUrl);
        Assert.Contains("CancelOrder", apiClient.LastUrl);
        Assert.DoesNotContain("CreateAndAssignOrder", apiClient.LastUrl);
    }

    [Fact]
    public async Task TestingDeployment_ProductionCancelIsRejectedBeforeRepositoryOrApi()
    {
        var repo = new FakeOrderRequestRepository();
        var apiClient = new FakeApiClient();
        var controller = BuildController(repo, apiClient);

        var error = await Assert.ThrowsAsync<RmsSupportHub.Api.Exceptions.EnvironmentNotAllowedException>(() =>
            controller.Cancel(
                "upc_ecommerce", 42,
                new OrderRequestCancelRequest("Customer request"),
                envKey: "UPC Production"));

        Assert.Equal("environment_not_allowed", error.Code);
        Assert.Null(apiClient.LastUrl);
        Assert.Empty(repo.ConnectionStrings);
    }

    [Fact]
    public async Task TestingDeployment_ProductionResendIsRejectedBeforeRepositoryOrApi()
    {
        var repo = new FakeOrderRequestRepository { Detail = MakeDetail(orderStatus: 2) };
        var apiClient = new FakeApiClient();
        var controller = BuildController(repo, apiClient);

        var error = await Assert.ThrowsAsync<RmsSupportHub.Api.Exceptions.EnvironmentNotAllowedException>(() =>
            controller.Resend(
                "upc_ecommerce", 42,
                new OrderRequestResendRequest("200"),
                envKey: "UPC Production"));

        Assert.Equal("environment_not_allowed", error.Code);
        Assert.Null(apiClient.LastUrl);
        Assert.Empty(repo.ConnectionStrings);
    }

    [Fact]
    public async Task ProductionCancelRequiresMutationUnlockBeforeReadingHistory()
    {
        var repo = new FakeOrderRequestRepository();
        var apiClient = new FakeApiClient();
        var gate = new ProductionMutationGate(
            "synthetic-owner-password",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductionMutationGate>.Instance);
        var controller = BuildController(
            repo,
            apiClient,
            DeploymentTier.Production,
            productionGate: gate);

        var error = await Assert.ThrowsAsync<RmsSupportHub.Api.Exceptions.ProductionMutationLockedException>(() =>
            controller.Cancel(
                "upc_ecommerce", 42,
                new OrderRequestCancelRequest("Customer request"),
                envKey: "UPC Production"));

        Assert.Equal("production_mutation_locked", error.Code);
        Assert.Empty(repo.ConnectionStrings);
        Assert.Null(apiClient.LastUrl);
    }

    [Fact]
    public async Task ProductionResendRequiresMutationUnlockBeforeReadingHistory()
    {
        var repo = new FakeOrderRequestRepository { Detail = MakeDetail(orderStatus: 2) };
        var apiClient = new FakeApiClient();
        var gate = new ProductionMutationGate(
            "synthetic-owner-password",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductionMutationGate>.Instance);
        var controller = BuildController(
            repo,
            apiClient,
            DeploymentTier.Production,
            productionGate: gate);

        var error = await Assert.ThrowsAsync<RmsSupportHub.Api.Exceptions.ProductionMutationLockedException>(() =>
            controller.Resend(
                "upc_ecommerce", 42,
                new OrderRequestResendRequest("200"),
                envKey: "UPC Production"));

        Assert.Equal("production_mutation_locked", error.Code);
        Assert.Empty(repo.ConnectionStrings);
        Assert.Null(apiClient.LastUrl);
    }

    [Fact]
    public async Task ProductionOrderRequestListRemainsReadOnlyWithoutMutationUnlock()
    {
        var repo = new FakeOrderRequestRepository();
        var controller = BuildController(
            repo,
            new FakeApiClient(),
            DeploymentTier.Production);

        var result = await controller.List(
            "upc_ecommerce",
            new OrderRequestListQuery(
                Q: null, OrderNumber: null, Phone: null, BranchCode: null,
                Status: null, Statuses: null, Succeeded: null, HasException: null,
                DateFrom: null, DateTo: null, Page: null, PageSize: null,
                Sort: null, ExactMatch: null),
            envKey: "UPC Production");

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        Assert.NotEmpty(repo.ConnectionStrings);
    }

    [Fact]
    public async Task UniCommerceCancelAndResendRemainCapabilityUnavailable()
    {
        var standard = new FakeOrderRequestRepository();
        var apiClient = new FakeApiClient();
        var controller = BuildController(standard, apiClient);

        var cancel = await controller.Cancel(
            "ghc_unicommerce", 42,
            new OrderRequestCancelRequest("synthetic"),
            envKey: "GHC Uni-Commerce Testing");
        var resend = await controller.Resend(
            "ghc_unicommerce", 42,
            new OrderRequestResendRequest("200"),
            envKey: "GHC Uni-Commerce Testing");

        Assert.Equal(501, Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ObjectResult>(cancel).StatusCode);
        Assert.Equal(501, Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ObjectResult>(resend).StatusCode);
        Assert.Null(apiClient.LastUrl);
    }

    [Fact]
    public async Task Cancel_OnAlreadyDoneOrder_Returns409WithoutCallingApi()
    {
        var repo = new FakeOrderRequestRepository { Detail = MakeDetail(orderStatus: 9) }; // 9 = Done, cancel-blocked
        var apiClient = new FakeApiClient();
        var controller = BuildController(repo, apiClient);

        var result = await controller.Cancel(
            "upc_ecommerce", 42,
            new OrderRequestCancelRequest("Customer request"),
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
        var controller = BuildController(repo, apiClient);

        var result = await controller.Resend(
            "upc_ecommerce", 42,
            new OrderRequestResendRequest("200"),
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
        var controller = BuildController(repo, apiClient);

        var result = await controller.Resend(
            "upc_ecommerce", 42,
            new OrderRequestResendRequest("200"),
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
        var controller = BuildController(repo, apiClient);

        await controller.Resend(
            "upc_ecommerce", 42,
            new OrderRequestResendRequest(null),
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
        var controller = BuildController(repo, apiClient);

        var result = await controller.Resend(
            "upc_ecommerce", 42,
            new OrderRequestResendRequest("200"),
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
        var controller = BuildController(repo, new FakeApiClient());
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
    public async Task UniCommerceList_UsesTheCapabilitySelectedReadOnlyAdapter()
    {
        var standard = new FakeOrderRequestRepository();
        var uni = new FakeGhcUnicommerceOrderRequestRepository();
        var controller = BuildController(standard, new FakeApiClient(), ghcUnicommerceRepository: uni);
        var query = new OrderRequestListQuery(
            Q: "UNI-1", OrderNumber: null, Phone: null, BranchCode: null,
            Status: null, Statuses: null, Succeeded: false, HasException: null,
            DateFrom: null, DateTo: null, Page: 1, PageSize: 25,
            Sort: null, ExactMatch: true);

        var result = await controller.List(
            "ghc_unicommerce", query, "GHC Uni-Commerce Testing");

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        Assert.Empty(standard.ConnectionStrings);
        Assert.NotEmpty(uni.ConnectionStrings);
        Assert.Equal("UNI-1", uni.LastFilters!.OrderNumber);
        Assert.False(uni.LastFilters.Succeeded == true);
    }

    [Fact]
    public async Task UniCommerceList_RejectsUnsupportedBranchFilterBeforeDatabaseReads()
    {
        var standard = new FakeOrderRequestRepository();
        var uni = new FakeGhcUnicommerceOrderRequestRepository();
        var controller = BuildController(standard, new FakeApiClient(), ghcUnicommerceRepository: uni);
        var query = new OrderRequestListQuery(
            Q: null, OrderNumber: null, Phone: null, BranchCode: "P001",
            Status: null, Statuses: null, Succeeded: null, HasException: null,
            DateFrom: null, DateTo: null, Page: 1, PageSize: 25,
            Sort: null, ExactMatch: true);

        var result = await controller.List(
            "ghc_unicommerce", query, "GHC Uni-Commerce Testing");

        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
        Assert.Empty(standard.ConnectionStrings);
        Assert.Empty(uni.ConnectionStrings);
    }

    [Fact]
    public async Task UniCommerceList_RejectsUnverifiedExceptionFilterBeforeDatabaseReads()
    {
        var standard = new FakeOrderRequestRepository();
        var uni = new FakeGhcUnicommerceOrderRequestRepository();
        var controller = BuildController(standard, new FakeApiClient(), ghcUnicommerceRepository: uni);
        var query = new OrderRequestListQuery(
            Q: null, OrderNumber: null, Phone: null, BranchCode: null,
            Status: null, Statuses: null, Succeeded: null, HasException: true,
            DateFrom: null, DateTo: null, Page: 1, PageSize: 25,
            Sort: null, ExactMatch: true);

        var result = await controller.List(
            "ghc_unicommerce", query, "GHC Uni-Commerce Testing");

        var badRequest = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
        Assert.Contains("exception state is unavailable", badRequest.Value?.ToString());
        Assert.Empty(standard.ConnectionStrings);
        Assert.Empty(uni.ConnectionStrings);
    }

    [Fact]
    public async Task ProductionListAndDetailReadsUseTheProductionCatalog()
    {
        var repo = new FakeOrderRequestRepository();
        var controller = BuildController(repo, new FakeApiClient(), DeploymentTier.Production);

        var list = await controller.List("upc_ecommerce", ListQuery(), "UPC Production");
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(list);

        var detail = await controller.GetDetail("upc_ecommerce", 42, "UPC Production");
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(detail);

        Assert.NotEmpty(repo.ConnectionStrings);
        foreach (var connectionString in repo.ConnectionStrings)
        {
            var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
            Assert.Equal("RmsMainProd", connection.InitialCatalog);
        }
    }

    [Fact]
    public async Task List_RejectsInvalidDateRangeBeforeStartingDatabaseReads()
    {
        var repo = new FakeOrderRequestRepository();
        var controller = BuildController(repo, new FakeApiClient());

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
        var controller = BuildController(repo, new FakeApiClient());

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
        var controller = BuildController(repo, new FakeApiClient());

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
        var controller = BuildController(repo, new FakeApiClient());

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
        var controller = BuildController(repo, new FakeApiClient());

        var result = await controller.List(
            "upc_ecommerce", ListQuery(page: 2, pageSize: 50), "UPC Testing");

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        Assert.Equal(1, repo.LastPage);
    }

    [Fact]
    public async Task List_ConvertsRepositoryFailureToRetryableUpstreamError()
    {
        var repo = new FakeOrderRequestRepository { ListException = new InvalidOperationException("secret SQL details") };
        var controller = BuildController(repo, new FakeApiClient());

        var error = await Assert.ThrowsAsync<RmsSupportHub.Api.Exceptions.UpstreamException>(() =>
            controller.List("upc_ecommerce", ListQuery(), "UPC Testing"));

        Assert.Equal(502, error.StatusCode);
        Assert.DoesNotContain("secret SQL details", error.Message);
    }
}
