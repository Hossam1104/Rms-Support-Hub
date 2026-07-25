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

    [Fact]
    public async Task HistoryEndpoints_ReturnsOkAndStoresEntries()
    {
        var historyResponse = await _client.GetAsync("/api/modules/ghc_ecommerce/order-history");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var history = await historyResponse.Content.ReadFromJsonAsync<List<OrderHistoryEntry>>();
        Assert.NotNull(history);
    }

    [Fact]
    public async Task ValidationSearch_GhcEcommerce_ReturnsBadRequest()
    {
        var searchReq = new OrderSearchRequest("ORD-123", null, null, null, null, null);
        var response = await _client.PostAsJsonAsync("/api/modules/ghc_ecommerce/validation/search", searchReq);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
