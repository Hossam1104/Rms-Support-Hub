using Moq;
using OnlineOrderTool.Api.Controllers;
using OnlineOrderTool.Core.DTOs;
using OnlineOrderTool.Core.Modules;
using OnlineOrderTool.Core.Repositories;
using OnlineOrderTool.Core.Services;
using OnlineOrderTool.Data;
using Xunit;

namespace OnlineOrderTool.Tests;

/// <summary>
/// U1 (UI_Rework_Plan.md D3): OrderController.CancelOrder used to resolve
/// <c>module.GetEnvironment(null)</c> unconditionally, so a cancel always hit
/// Production's CancelUrl no matter which environment the operator had
/// selected or which one the original send used. CancelOrderRequest now
/// carries EnvironmentKey and the controller threads it through -- this
/// proves it with a stubbed IApiClient rather than a live call, per this
/// repository's standing rule that every live call goes to UPC Testing only.
/// </summary>
public class OrderControllerEnvironmentTests
{
    private static OrderController BuildController(out Mock<IApiClient> apiClient, out string? capturedUrl)
    {
        var module = new UpcEcommerceModule(
            new FlatOrderPayloadBuilder(),
            new FlatOrderValidator(),
            Mock.Of<IItemRepository>(),
            Mock.Of<IConsumerRepository>());

        var registry = new Mock<IModuleRegistry>();
        registry.Setup(r => r.GetModule("upc_ecommerce")).Returns(module);

        string? url = null;
        var client = new Mock<IApiClient>();
        client.Setup(c => c.SendOrderAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, object>((u, _) => url = u)
            .ReturnsAsync(new ApiResponseResult(200, "{}", "unused", true));

        apiClient = client;
        capturedUrl = null;

        var controller = new OrderController(
            registry.Object,
            Mock.Of<IDraftManager>(),
            client.Object,
            Mock.Of<ISqlServerConnectionFactory>());

        return controller;
    }

    [Fact]
    public async Task CancelOrder_WithTestingEnvironmentKey_PostsToTestingCancelUrl_NotProduction()
    {
        var controller = BuildController(out var apiClient, out _);

        var request = new CancelOrderRequest("ORD123", "customer request", "UPC Testing");
        await controller.CancelOrder("upc_ecommerce", request);

        apiClient.Verify(c => c.SendOrderAsync(
            "http://10.10.10.181:8080/RmsMainServerApi/api/Order/CancelOrder",
            It.IsAny<object>()), Times.Once);

        apiClient.Verify(c => c.SendOrderAsync(
            "https://10.10.10.181/RmsMainServerApi/api/Order/CancelOrder",
            It.IsAny<object>()), Times.Never);

        apiClient.Verify(c => c.SendOrderAsync(
            "http://10.10.10.181:8080/RmsMainServerApi/api/Order/CreateAndAssignOrder",
            It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task CancelOrder_WithNoEnvironmentKey_DefaultsToTesting_NotProduction()
    {
        var controller = BuildController(out var apiClient, out _);

        var request = new CancelOrderRequest("ORD123", "customer request", null);
        await controller.CancelOrder("upc_ecommerce", request);

        apiClient.Verify(c => c.SendOrderAsync(
            "http://10.10.10.181:8080/RmsMainServerApi/api/Order/CancelOrder",
            It.IsAny<object>()), Times.Once);
    }
}
