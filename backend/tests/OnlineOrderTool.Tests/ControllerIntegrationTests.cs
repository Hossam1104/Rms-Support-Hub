using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OnlineOrderTool.Core.DTOs;
using OnlineOrderTool.Core.Models;
using Xunit;

namespace OnlineOrderTool.Tests;

public class ControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetModules_ReturnsOkAndAllFiveModules()
    {
        var response = await _client.GetAsync("/api/modules");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var modules = await response.Content.ReadFromJsonAsync<List<ModuleDto>>();
        Assert.NotNull(modules);
        Assert.Equal(5, modules.Count);
    }

    [Fact]
    public async Task AddProduct_AddsProductToDraftState()
    {
        var product = new Product
        {
            ItemCode = "TEST-100",
            ItemName = "Test Product",
            Quantity = 2m,
            UnitPrice = 50m,
            VatPercentage = 15m
        };

        var response = await _client.PostAsJsonAsync("/api/modules/ghc_ecommerce/products", product);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stateResponse = await _client.GetAsync("/api/modules/ghc_ecommerce/state");
        var draft = await stateResponse.Content.ReadFromJsonAsync<OrderDraft>();
        Assert.NotNull(draft);
        Assert.Contains(draft.Products, p => p.ItemCode == "TEST-100");
    }

    [Fact]
    public async Task AddPayment_UpcPostToCredit_ReturnsBadRequest()
    {
        var payment = new Payment
        {
            PaymentMethod = "PostToCredit",
            PaymentStatus = "done_payment",
            PaymentAmount = 100m
        };

        var response = await _client.PostAsJsonAsync("/api/modules/upc_ecommerce/payments", payment);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>The order-history JSON-file feature (OrderHistoryService,
    /// HistoryController, order_history_*.json) was retired in R5 in favor of
    /// OrderRequestsController reading the real OrderRequests table -- the
    /// route must be gone, not just empty.</summary>
    [Fact]
    public async Task OldHistoryRoute_NoLongerExists()
    {
        var response = await _client.GetAsync("/api/modules/ghc_ecommerce/order-history");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>ValidationController/IOrderValidationRepository/
    /// UpcOrderValidationRepository (which read RequestOrderHeaders-first and
    /// never surfaced ResponseJson/ExceptionMessage) were deleted in R5 in
    /// favor of OrderRequestsController.</summary>
    [Fact]
    public async Task OldValidationSearchRoute_NoLongerExists()
    {
        var response = await _client.PostAsJsonAsync("/api/modules/ghc_ecommerce/validation/search", new { orderNumber = "ORD-123" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>GHC has Capabilities.OrderRequests = false (database
    /// credentials pending, see GhcEcommerceModule's TODO(db-creds)), so the
    /// new order-requests surface must 501, not silently return empty/wrong
    /// data for a module it isn't wired up for.</summary>
    [Fact]
    public async Task OrderRequests_GhcEcommerce_Returns501()
    {
        var response = await _client.GetAsync("/api/modules/ghc_ecommerce/order-requests");
        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }
}
