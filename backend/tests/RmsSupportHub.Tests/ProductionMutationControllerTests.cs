using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RmsSupportHub.Api;
using RmsSupportHub.Api.Controllers;
using RmsSupportHub.Api.Exceptions;
using RmsSupportHub.Api.Middleware;
using RmsSupportHub.Api.Security;
using RmsSupportHub.Core.DTOs;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Repositories;
using RmsSupportHub.Core.Services;
using RmsSupportHub.Data;

namespace RmsSupportHub.Tests;

public sealed class ProductionMutationControllerTests
{
    private const string Password = "synthetic-owner-password";

    [Fact]
    public async Task OrderCancelBlocksProductionWithoutUnlockAndAllowsAValidScopedToken()
    {
        var module = new UpcEcommerceModule(
            new FlatOrderPayloadBuilder(),
            new FlatOrderValidator(),
            Mock.Of<IItemRepository>(),
            Mock.Of<IConsumerRepository>(),
            TestEnvironmentCatalog.Upc());
        var registry = new Mock<IModuleRegistry>();
        registry.Setup(item => item.GetModule("upc_ecommerce")).Returns(module);

        var apiClient = new Mock<IApiClient>();
        apiClient.Setup(client => client.SendOrderAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new ApiResponseResult(200, "{\"ok\":true}", "synthetic", true));
        var gate = new ProductionMutationGate(Password, NullLogger<ProductionMutationGate>.Instance);
        var controller = new OrderController(
            registry.Object,
            Mock.Of<IDraftManager>(),
            apiClient.Object,
            Mock.Of<ISqlServerConnectionFactory>(),
            new ServerConnectionStringResolver(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
            new EnvironmentPolicy(DeploymentTier.Production),
            gate,
            new RmsSupportHub.Api.Configuration.EmptyOutboundApiKeyResolver());
        var httpContext = new DefaultHttpContext();
        httpContext.Items[SessionIdMiddleware.CookieName] = "direct-controller-session";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var locked = await Assert.ThrowsAsync<ProductionMutationLockedException>(() => controller.CancelOrder(
            "upc_ecommerce",
            new CancelOrderRequest("ORD-1", "synthetic test", "UPC Production")));
        Assert.Equal("production_mutation_locked", locked.Code);
        apiClient.Verify(client => client.SendOrderAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);

        var unlock = gate.Unlock("upc_ecommerce", "direct-controller-session", Password);
        httpContext.Request.Headers[ProductionMutationGate.UnlockHeaderName] = unlock.Token;

        var result = await controller.CancelOrder(
            "upc_ecommerce",
            new CancelOrderRequest("ORD-1", "synthetic test", "UPC Production"));

        Assert.IsType<OkObjectResult>(result);
        apiClient.Verify(client => client.SendOrderAsync(
            "http://10.10.10.181/RmsMainServerApi/api/Order/CancelOrder",
            It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public void ProductionUnlockEndpointReturnsOnlyAnOpaqueToken()
    {
        var module = new UpcEcommerceModule(
            new FlatOrderPayloadBuilder(),
            new FlatOrderValidator(),
            Mock.Of<IItemRepository>(),
            Mock.Of<IConsumerRepository>(),
            TestEnvironmentCatalog.Upc());
        var registry = new Mock<IModuleRegistry>();
        registry.Setup(item => item.GetModule("upc_ecommerce")).Returns(module);
        var gate = new ProductionMutationGate(Password, NullLogger<ProductionMutationGate>.Instance);
        var controller = new OrderController(
            registry.Object,
            Mock.Of<IDraftManager>(),
            Mock.Of<IApiClient>(),
            Mock.Of<ISqlServerConnectionFactory>(),
            new ServerConnectionStringResolver(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
            new EnvironmentPolicy(DeploymentTier.Production),
            gate);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var action = controller.ProductionUnlock(
            "upc_ecommerce",
            new ProductionUnlockRequest(Password));

        var response = Assert.IsType<OkObjectResult>(action.Result);
        var body = Assert.IsType<ProductionUnlockResponse>(response.Value);
        Assert.NotEqual(Password, body.Token);
        Assert.NotEmpty(body.Token);
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ProductionSendWithAValidTokenReachesTheMockedOutboundLayer()
    {
        var module = new UpcEcommerceModule(
            new FlatOrderPayloadBuilder(),
            new FlatOrderValidator(),
            Mock.Of<IItemRepository>(),
            Mock.Of<IConsumerRepository>(),
            TestEnvironmentCatalog.Upc());
        var registry = new Mock<IModuleRegistry>();
        registry.Setup(item => item.GetModule("upc_ecommerce")).Returns(module);
        var apiClient = new Mock<IApiClient>();
        apiClient.Setup(client => client.SendOrderAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new ApiResponseResult(200, "{\"ok\":true}", "synthetic", true));
        var draftManager = new Mock<IDraftManager>();
        draftManager.Setup(manager => manager.LoadDraftAsync(It.IsAny<string>(), "upc_ecommerce"))
            .ReturnsAsync(new OrderDraft
            {
                OrderData = new Dictionary<string, object?>
                {
                    ["branch_code"] = "201",
                    ["order_code"] = "UPC-TEST-1",
                    ["client_first_name"] = "Test",
                    ["client_last_name"] = "Operator",
                    ["client_phone"] = "0500000000",
                    ["order_address"] = "Test address",
                    ["is_delivery"] = false
                },
                Products = new List<Product>
                {
                    new() { ItemCode = "200001", ItemName = "Test product", Quantity = 1m, UnitPrice = 10m, VatPercentage = 15m }
                }
            });
        var gate = new ProductionMutationGate(Password, NullLogger<ProductionMutationGate>.Instance);
        var controller = new OrderController(
            registry.Object,
            draftManager.Object,
            apiClient.Object,
            Mock.Of<ISqlServerConnectionFactory>(),
            new ServerConnectionStringResolver(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
            new EnvironmentPolicy(DeploymentTier.Production),
            gate);
        var httpContext = new DefaultHttpContext();
        httpContext.Items[SessionIdMiddleware.CookieName] = "direct-controller-session";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var locked = await Assert.ThrowsAsync<ProductionMutationLockedException>(() => controller.SendRequest(
            "upc_ecommerce",
            new SendOrderRequest("UPC Production")));
        Assert.Equal("production_mutation_locked", locked.Code);
        apiClient.Verify(client => client.SendOrderAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);

        var unlock = gate.Unlock("upc_ecommerce", "direct-controller-session", Password);
        httpContext.Request.Headers[ProductionMutationGate.UnlockHeaderName] = unlock.Token;

        var result = await controller.SendRequest(
            "upc_ecommerce",
            new SendOrderRequest("UPC Production"));

        Assert.IsType<OkObjectResult>(result);
        apiClient.Verify(client => client.SendOrderAsync(
            "http://10.10.10.181/RmsMainServerApi/api/Order/CreateAndAssignOrder",
            It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task RequiredUniApiKeyMissingFailsClosedBeforeOutboundSend()
    {
        var environments = ModuleEnvironmentDefaults.GhcUnicommerce()
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value with
                {
                    Available = true,
                    ApiUrl = "https://uni.testing.example/create",
                    ConnectionStringName = "GhcUnicommerceTest",
                    ApiKeyConfigurationKey = "UniTestingApiKey"
                },
                StringComparer.OrdinalIgnoreCase);
        var module = new GhcUnicommerceModule(
            new UniCommercePayloadBuilder(),
            new UniCommerceValidator(),
            Mock.Of<IGhcUnicommerceConsumerRepository>(),
            environments);
        var registry = new Mock<IModuleRegistry>();
        registry.Setup(item => item.GetModule("ghc_unicommerce")).Returns(module);
        var apiClient = new Mock<IApiClient>();
        var draftManager = new Mock<IDraftManager>();
        draftManager.Setup(manager => manager.LoadDraftAsync(It.IsAny<string>(), "ghc_unicommerce"))
            .ReturnsAsync(new OrderDraft
            {
                OrderData = new Dictionary<string, object?>
                {
                    ["reference_number"] = "UNI-TEST-1",
                    ["customer_name"] = "AMAZON"
                },
                RowItems = new List<RowItem>
                {
                    new() { MaterialNumber = "MAT-1", Quantity = 1m, ItemPrice = 34.25m, VatPercentage = 15m }
                }
            });
        var gate = new ProductionMutationGate(Password, NullLogger<ProductionMutationGate>.Instance);
        var controller = new OrderController(
            registry.Object,
            draftManager.Object,
            apiClient.Object,
            Mock.Of<ISqlServerConnectionFactory>(),
            new ServerConnectionStringResolver(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
            new EnvironmentPolicy(DeploymentTier.Production),
            gate,
            new RmsSupportHub.Api.Configuration.EmptyOutboundApiKeyResolver());
        var httpContext = new DefaultHttpContext();
        httpContext.Items[SessionIdMiddleware.CookieName] = "direct-controller-session";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        var unlock = gate.Unlock("ghc_unicommerce", "direct-controller-session", Password);
        httpContext.Request.Headers[ProductionMutationGate.UnlockHeaderName] = unlock.Token;

        var error = await Assert.ThrowsAsync<EnvironmentUnconfiguredException>(() => controller.SendRequest(
            "ghc_unicommerce",
            new SendOrderRequest("GHC Uni-Commerce Production")));

        Assert.Equal("environment_unconfigured", error.Code);
        apiClient.Verify(client => client.SendOrderWithApiKeyAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()), Times.Never);
        apiClient.Verify(client => client.SendOrderAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }
}
