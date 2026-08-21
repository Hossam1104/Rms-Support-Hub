using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RmsSupportHub.Api.Exceptions;
using RmsSupportHub.Api.Security;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Modules;

namespace RmsSupportHub.Tests;

public sealed class ProductionMutationGateTests
{
    private const string Password = "synthetic-owner-password";

    [Fact]
    public void MissingOwnerSecretReturnsSafeConfigurationError()
    {
        var gate = new ProductionMutationGate(null, NullLogger<ProductionMutationGate>.Instance);

        var error = Assert.Throws<ProductionUnlockUnavailableException>(
            () => gate.Unlock("upc_ecommerce", "session-1", Password));

        Assert.Equal("production_unlock_unavailable", error.Code);
        Assert.DoesNotContain(Password, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WrongPasswordReturnsGenericFailureWithoutEchoingSecret()
    {
        var gate = new ProductionMutationGate(Password, NullLogger<ProductionMutationGate>.Instance);

        var error = Assert.Throws<ProductionUnlockFailedException>(
            () => gate.Unlock("upc_ecommerce", "session-1", "wrong-password"));

        Assert.Equal("production_unlock_failed", error.Code);
        Assert.DoesNotContain("wrong-password", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("upc_ecommerce")]
    [InlineData("ghc_ecommerce")]
    [InlineData("ghc_unicommerce")]
    public void CorrectTokenAllowsEachSupportedProductionModule(string moduleKey)
    {
        var gate = new ProductionMutationGate(
            Password,
            NullLogger<ProductionMutationGate>.Instance);
        var module = Mock.Of<IOrderModule>(m => m.Key == moduleKey);
        var environment = ProductionEnvironment("Synthetic Production");

        var unlock = gate.Unlock(module.Key, "session-1", Password);

        Assert.NotEqual(Password, unlock.Token);
        Assert.NotEmpty(unlock.Token);
        gate.RequireUnlocked(module, environment, unlock.Token, "session-1", "send");
    }

    [Fact]
    public void TokenScopeAndExpiryBlockReuse()
    {
        var now = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        var gate = new ProductionMutationGate(
            Password,
            NullLogger<ProductionMutationGate>.Instance,
            () => now);
        var module = Mock.Of<IOrderModule>(m => m.Key == "upc_ecommerce");
        var environment = ProductionEnvironment("UPC Production");
        var unlock = gate.Unlock(module.Key, "session-1", Password);

        Assert.Throws<ProductionMutationLockedException>(
            () => gate.RequireUnlocked(module, environment, unlock.Token, "session-2", "send"));

        now = now.AddMinutes(10);
        var expired = Assert.Throws<ProductionUnlockExpiredException>(
            () => gate.RequireUnlocked(module, environment, unlock.Token, "session-1", "send"));
        Assert.Equal("production_unlock_expired", expired.Code);
    }

    [Fact]
    public void FailedUnlockAttemptsAreThrottledPerModuleAndSession()
    {
        var gate = new ProductionMutationGate(Password, NullLogger<ProductionMutationGate>.Instance);

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var error = Assert.Throws<ProductionUnlockFailedException>(
                () => gate.Unlock("upc_ecommerce", "session-1", "wrong-password"));
            Assert.Equal("production_unlock_failed", error.Code);
        }
    }

    private static ModuleEnvironment ProductionEnvironment(string key) => new()
    {
        Key = key,
        Environment = "Production",
        Description = "Synthetic test environment",
        Accent = "test",
        Cue = "Test",
        Icon = "bi-shield",
        RouteLabel = "Live lane",
        VisualUrl = "",
        VisualAlt = "",
        Available = true
    };
}
