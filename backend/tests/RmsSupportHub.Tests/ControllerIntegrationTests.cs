using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RmsSupportHub.Core.DTOs;
using RmsSupportHub.Core.Models;
using Xunit;

namespace RmsSupportHub.Tests;

public class ControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
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

    /// <summary>Proves the R6 fix for remediation_plan.md B19: DraftManager
    /// used to be a process-global singleton file, so two browsers editing
    /// the same module silently overwrote each other. Each WebApplicationFactory
    /// client here is a separate cookie jar, i.e. a separate "browser".</summary>
    [Fact]
    public async Task TwoSeparateSessions_HaveIndependentDraftState()
    {
        using var clientA = _factory.CreateClient();
        using var clientB = _factory.CreateClient();

        var productA = new Product { ItemCode = "SESSION-A-ITEM", ItemName = "A", Quantity = 1m, UnitPrice = 10m };
        var productB = new Product { ItemCode = "SESSION-B-ITEM", ItemName = "B", Quantity = 1m, UnitPrice = 20m };

        var addA = await clientA.PostAsJsonAsync("/api/modules/ghc_ecommerce/products", productA);
        Assert.Equal(HttpStatusCode.OK, addA.StatusCode);

        var addB = await clientB.PostAsJsonAsync("/api/modules/ghc_ecommerce/products", productB);
        Assert.Equal(HttpStatusCode.OK, addB.StatusCode);

        var stateA = await (await clientA.GetAsync("/api/modules/ghc_ecommerce/state")).Content.ReadFromJsonAsync<OrderDraft>();
        var stateB = await (await clientB.GetAsync("/api/modules/ghc_ecommerce/state")).Content.ReadFromJsonAsync<OrderDraft>();

        Assert.Contains(stateA!.Products, p => p.ItemCode == "SESSION-A-ITEM");
        Assert.DoesNotContain(stateA.Products, p => p.ItemCode == "SESSION-B-ITEM");

        Assert.Contains(stateB!.Products, p => p.ItemCode == "SESSION-B-ITEM");
        Assert.DoesNotContain(stateB.Products, p => p.ItemCode == "SESSION-A-ITEM");
    }

    /// <summary>U2 (UI_Rework_Plan.md D1): PATCH order-data is the batched
    /// replacement for one-PUT-per-field -- a single call carrying several
    /// fields must apply every one of them in one load-modify-write.</summary>
    [Fact]
    public async Task PatchOrderData_AppliesMultiFieldBodyInOneCall()
    {
        var body = new
        {
            fields = new Dictionary<string, object?>
            {
                ["client_first_name"] = "Mohamed",
                ["client_last_name"] = "Elbanna",
                ["client_phone"] = "0556028080",
                ["order_address"] = "Test Address"
            }
        };

        var response = await _client.PatchAsJsonAsync("/api/modules/upc_ecommerce/order-data", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stateResponse = await _client.GetAsync("/api/modules/upc_ecommerce/state");
        var draft = await stateResponse.Content.ReadFromJsonAsync<OrderDraft>();

        Assert.NotNull(draft);
        Assert.Equal("Mohamed", draft!.OrderData["client_first_name"]?.ToString());
        Assert.Equal("Elbanna", draft.OrderData["client_last_name"]?.ToString());
        Assert.Equal("0556028080", draft.OrderData["client_phone"]?.ToString());
        Assert.Equal("Test Address", draft.OrderData["order_address"]?.ToString());
    }

    /// <summary>An empty field set is a caller error, not a silent no-op.</summary>
    [Fact]
    public async Task PatchOrderData_EmptyFields_ReturnsBadRequest()
    {
        var body = new { fields = new Dictionary<string, object?>() };
        var response = await _client.PatchAsJsonAsync("/api/modules/upc_ecommerce/order-data", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>U4 (UI_Rework_Plan.md D13): the temporary U2 compatibility
    /// adapter is retired -- the batched PATCH order-data route (covered by
    /// PatchOrderData_AppliesMultiFieldBodyInOneCall above) is the only
    /// order-data write path, so the old route must be gone, not just empty.</summary>
    [Fact]
    public async Task OldOrderFieldRoute_NoLongerExists()
    {
        var body = new { fieldName = "branch_code", value = "101" };
        var response = await _client.PutAsJsonAsync("/api/modules/upc_ecommerce/order-field", body);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>U4: the operator must see where Send will post. The endpoint
    /// route returns the resolved environment's ApiUrl (Testing by default)
    /// -- scoped to the single active environment, unlike the module catalog
    /// which never carries URLs (B16).</summary>
    [Fact]
    public async Task GetEndpoint_ReturnsResolvedEnvironmentApiUrl()
    {
        var response = await _client.GetAsync("/api/modules/upc_ecommerce/endpoint");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var endpoint = await response.Content.ReadFromJsonAsync<ModuleEndpointDto>();
        Assert.NotNull(endpoint);
        Assert.Equal("UPC Testing", endpoint!.EnvironmentKey);
        Assert.Equal("Testing", endpoint.Environment);
        Assert.False(string.IsNullOrWhiteSpace(endpoint.ApiUrl));
    }

    /// <summary>The same GetEnvironment resolution send-request uses applies
    /// here too -- an explicit envKey selects that environment's URL.</summary>
    [Fact]
    public async Task GetEndpoint_HonoursExplicitEnvKey()
    {
        var response = await _client.GetAsync("/api/modules/upc_ecommerce/endpoint?envKey=UPC%20Production");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var endpoint = await response.Content.ReadFromJsonAsync<ModuleEndpointDto>();
        Assert.NotNull(endpoint);
        Assert.Equal("UPC Production", endpoint!.EnvironmentKey);
        Assert.Equal("Production", endpoint.Environment);
    }

    /// <summary>The display contract must not become a new topology leak:
    /// no CancelUrl, connection-string name, or database config is emitted.</summary>
    [Fact]
    public async Task GetEndpoint_NeverExposesCancelUrlOrConnectionInfo()
    {
        var response = await _client.GetAsync("/api/modules/upc_ecommerce/endpoint");
        var raw = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(raw);
        Assert.False(doc.RootElement.TryGetProperty("cancelUrl", out _), "endpoint.cancelUrl must not be exposed.");
        Assert.False(doc.RootElement.TryGetProperty("connectionStringName", out _), "endpoint.connectionStringName must not be exposed.");
        Assert.False(doc.RootElement.TryGetProperty("dbConfig", out _), "endpoint.dbConfig must not be exposed.");
    }

    [Fact]
    public async Task GetEndpoint_UnknownModule_Returns404()
    {
        var response = await _client.GetAsync("/api/modules/no_such_module/endpoint");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Proves the R6 fix for remediation_plan.md B16: internal RMS
    /// endpoint topology (ApiUrl/CancelUrl) is never published to the
    /// browser -- only the boolean availability of each.</summary>
    [Fact]
    public async Task GetModules_NeverExposesApiUrlOrCancelUrl()
    {
        var response = await _client.GetAsync("/api/modules");
        var raw = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(raw);
        foreach (var module in doc.RootElement.EnumerateArray())
        {
            foreach (var env in module.GetProperty("environments").EnumerateArray())
            {
                Assert.False(env.TryGetProperty("apiUrl", out _), "environments[].apiUrl must not be exposed to the browser.");
                Assert.False(env.TryGetProperty("cancelUrl", out _), "environments[].cancelUrl must not be exposed to the browser.");
                Assert.True(env.TryGetProperty("hasApiUrl", out _), "environments[].hasApiUrl should be present.");
                Assert.True(env.TryGetProperty("hasCancelUrl", out _), "environments[].hasCancelUrl should be present.");
            }
        }
    }

    [Fact]
    public async Task NonExistentApiRoute_DeleteAndPatch_Returns404()
    {
        var deleteResponse = await _client.DeleteAsync("/api/modules/ghc_ecommerce/nonexistent-endpoint");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        var patchResponse = await _client.PatchAsJsonAsync("/api/modules/ghc_ecommerce/nonexistent-endpoint", new { });
        Assert.Equal(HttpStatusCode.NotFound, patchResponse.StatusCode);
    }

    [Fact]
    public async Task NonApiDeepLink_DoesNotReturn405MethodNotAllowed()
    {
        var getResponse = await _client.GetAsync("/tools/pos-maintenance");
        Assert.NotEqual(HttpStatusCode.MethodNotAllowed, getResponse.StatusCode);

        var postResponse = await _client.PostAsJsonAsync("/tools/pos-maintenance", new { });
        Assert.Equal(HttpStatusCode.NotFound, postResponse.StatusCode);
    }
}
