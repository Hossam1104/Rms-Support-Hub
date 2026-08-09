using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RmsSupportHub.Api.Controllers;
using RmsSupportHub.Core.DTOs;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Repositories;
using RmsSupportHub.Core.Services;
using RmsSupportHub.Data;
using Xunit;

namespace RmsSupportHub.Tests;

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
            "http://10.10.10.181/RmsMainServerApi/api/Order/CancelOrder",
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

    [Fact]
    public void GetEndpoint_ResolvesProductionUrlWithoutPosting()
    {
        var controller = BuildController(out var apiClient, out _);

        var result = controller.GetEndpoint("upc_ecommerce", "UPC Production");
        var ok = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        var endpoint = Assert.IsType<ModuleEndpointDto>(ok.Value);

        Assert.Equal("http://10.10.10.181/RmsMainServerApi/api/Order/CreateAndAssignOrder", endpoint.ApiUrl);
        Assert.DoesNotContain(":8080", endpoint.ApiUrl);
        apiClient.Verify(c => c.SendOrderAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task SendRequest_ProductionIgnoresBrowserCustomUrl()
    {
        var production = new ModuleEnvironment
        {
            Key = "UPC Production",
            Environment = "Production",
            Description = "",
            Accent = "",
            Cue = "",
            Icon = "",
            RouteLabel = "",
            VisualUrl = "",
            VisualAlt = "",
            Available = true,
            ApiUrl = "http://10.10.10.181/RmsMainServerApi/api/Order/CreateAndAssignOrder",
            AllowCustomApiUrl = false
        };
        var module = new Mock<IOrderModule>();
        module.SetupGet(m => m.Key).Returns("upc_ecommerce");
        module.Setup(m => m.GetEnvironment("UPC Production")).Returns(production);
        module.Setup(m => m.Validate(It.IsAny<OrderDraft>())).Returns(new List<string>());
        module.Setup(m => m.BuildPayload(It.IsAny<OrderDraft>())).Returns(new Dictionary<string, object?>());

        var registry = new Mock<IModuleRegistry>();
        registry.Setup(r => r.GetModule("upc_ecommerce")).Returns(module.Object);

        var apiClient = new Mock<IApiClient>();
        apiClient.Setup(c => c.SendOrderAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new ApiResponseResult(200, "{}", "", true));
        var drafts = new Mock<IDraftManager>();
        drafts.Setup(d => d.LoadDraftAsync(It.IsAny<string>(), "upc_ecommerce"))
            .ReturnsAsync(new OrderDraft());

        var controller = new OrderController(
            registry.Object,
            drafts.Object,
            apiClient.Object,
            Mock.Of<ISqlServerConnectionFactory>());
        var context = new DefaultHttpContext();
        context.Items["oot_sid"] = "test-session";
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        await controller.SendRequest(
            "upc_ecommerce",
            new SendOrderRequest("UPC Production", "https://attacker.example/create"));

        apiClient.Verify(c => c.SendOrderAsync(
            "http://10.10.10.181/RmsMainServerApi/api/Order/CreateAndAssignOrder",
            It.IsAny<object>()), Times.Once);
        apiClient.Verify(c => c.SendOrderAsync(
            "https://attacker.example/create",
            It.IsAny<object>()), Times.Never);
    }
}
