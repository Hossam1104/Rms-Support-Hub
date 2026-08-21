using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using RmsSupportHub.Core.DTOs;
using RmsSupportHub.Core.Models;
using Xunit;

namespace RmsSupportHub.Tests;

public class ControllerIntegrationTests : IClassFixture<TestingWebApplicationFactory>
{
    private readonly TestingWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ControllerIntegrationTests(TestingWebApplicationFactory factory)
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
    public async Task GetModules_ReportsEffectiveTestingCatalogAndCapabilityTruth()
    {
        var response = await _client.GetAsync("/api/modules");
        var modules = await response.Content.ReadFromJsonAsync<List<ModuleDto>>();
        Assert.NotNull(modules);

        var upc = modules!.Single(module => module.Key == "upc_ecommerce");
        Assert.True(upc.Available);
        Assert.True(upc.Environments.Single(environment => environment.Key == "UPC Testing").Available);
        Assert.False(upc.Environments.Single(environment => environment.Key == "UPC Production").Available);

        var ghc = modules.Single(module => module.Key == "ghc_ecommerce");
        Assert.False(ghc.Available);
        Assert.False(ghc.Capabilities.Resend);
        Assert.All(ghc.Environments, environment => Assert.False(environment.Available));

        var uni = modules.Single(module => module.Key == "ghc_unicommerce");
        Assert.False(uni.Available);
        Assert.All(uni.Environments, environment => Assert.False(environment.Available));

        Assert.False(modules.Single(module => module.Key == "oms").Available);
        Assert.False(modules.Single(module => module.Key == "call_center").Available);
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

    /// <summary>GHC now uses the shared OrderRequests workflow. With no
    /// connection string in this isolated test host, the route fails at the
    /// environment boundary rather than pretending the history is empty.</summary>
    [Fact]
    public async Task OrderRequests_GhcEcommerce_RequiresConfiguredTestingDatabase()
    {
        var response = await _client.GetAsync("/api/modules/ghc_ecommerce/order-requests");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
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

    /// <summary>The complete draft route preserves Uni-Commerce state that is
    /// outside OrderData, including row items and consumer/delivery details.</summary>
    [Fact]
    public async Task PutState_PreservesCompleteUniCommerceDraft()
    {
        var draft = new OrderDraft
        {
            OrderData = new Dictionary<string, object?>
            {
                ["reference_number"] = "STATE-TEST-001",
                ["online_order_number"] = "STATE-TEST-001"
            },
            Consumer = new Consumer
            {
                FirstName = "State",
                LastName = "Test",
                PrimaryPhoneNumber = "000000000"
            },
            Delivery = new DeliveryDetails
            {
                DeliveryAddress = "Testing address",
                DeliveryFees = 2m
            },
            RowItems = new List<RowItem>
            {
                new()
                {
                    Quantity = 1m,
                    MaterialNumber = "STATE-ITEM",
                    Barcode = "STATE-ITEM",
                    ItemPrice = 10m
                }
            }
        };

        var response = await _client.PutAsJsonAsync("/api/modules/ghc_unicommerce/state", draft);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stateResponse = await _client.GetAsync("/api/modules/ghc_unicommerce/state");
        var saved = await stateResponse.Content.ReadFromJsonAsync<OrderDraft>();

        Assert.NotNull(saved);
        Assert.Equal("STATE-TEST-001", saved!.OrderData["reference_number"]?.ToString());
        Assert.Equal("State", saved.Consumer.FirstName);
        Assert.Equal("Testing address", saved.Delivery.DeliveryAddress);
        Assert.Single(saved.RowItems);
        Assert.Equal("STATE-ITEM", saved.RowItems[0].MaterialNumber);
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

    /// <summary>The Testing deployment policy is server-owned: a handcrafted
    /// Production endpoint request is rejected even though that environment
    /// exists in the registered catalog.</summary>
    [Fact]
    public async Task GetEndpoint_RejectsProductionInTestingDeployment()
    {
        var response = await _client.GetAsync("/api/modules/upc_ecommerce/endpoint?envKey=UPC%20Production");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("environment_not_allowed", error.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TestEndpoint_RejectsProductionBeforeAnyProbeAndIgnoresArbitraryUrl()
    {
        var response = await _client.PostAsync(
            "/api/modules/upc_ecommerce/test-endpoint?envKey=UPC%20Production&url=https%3A%2F%2Fattacker.example%2Fprobe",
            content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("environment_not_allowed", body.GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("attacker.example", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TestDb_IgnoresRawConnectionStringAndUsesServerConfigurationOnly()
    {
        using var client = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UpcEcommerceTest"] = ""
            }))).CreateClient();

        var response = await client.PostAsync(
            "/api/modules/upc_ecommerce/test-db?envKey=UPC%20Testing&connectionString=Server%3Dattacker%3BDatabase%3DEvil%3B",
            content: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("attacker", raw, StringComparison.OrdinalIgnoreCase);
        using var body = JsonDocument.Parse(raw);
        Assert.Equal("environment_unconfigured", body.RootElement.GetProperty("error").GetProperty("code").GetString());
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

/// <summary>Supplies a synthetic server-side secret so integration tests do
/// not depend on developer user-secrets or advertise a real database as a
/// prerequisite. The test never opens this connection; tests that need the
/// missing-secret path override it with an empty value.</summary>
public sealed class TestingWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UpcEcommerceTest"] =
                    "Server=127.0.0.1;Database=RmsSupportHubTest;Integrated Security=True;TrustServerCertificate=True;"
            }));
    }
}
