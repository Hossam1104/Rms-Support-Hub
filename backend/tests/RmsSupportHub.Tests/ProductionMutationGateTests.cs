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
    public void CorrectPasswordIsRejectedDuringThrottleAndSucceedsAfterExpiry()
    {
        var now = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        var gate = new ProductionMutationGate(
            Password,
            NullLogger<ProductionMutationGate>.Instance,
            () => now);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var error = Assert.Throws<ProductionUnlockFailedException>(
                () => gate.Unlock("upc_ecommerce", "session-1", "wrong-password"));
            Assert.Equal("production_unlock_failed", error.Code);
        }

        Assert.Throws<ProductionUnlockFailedException>(
            () => gate.Unlock("upc_ecommerce", "session-1", Password));

        now = now.AddMinutes(1).AddSeconds(1);
        var unlock = gate.Unlock("upc_ecommerce", "session-1", Password);
        Assert.NotEmpty(unlock.Token);
    }

    [Fact]
    public void ExpiredAttemptWindowResetsWithoutSpinning()
    {
        var now = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        var gate = new ProductionMutationGate(
            Password,
            NullLogger<ProductionMutationGate>.Instance,
            () => now);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Throws<ProductionUnlockFailedException>(() => gate.Unlock(
                "upc_ecommerce", "session-1", "wrong-password", "198.51.100.10"));
        }

        now = now.AddMinutes(1).AddSeconds(1);

        var unlock = gate.Unlock(
            "upc_ecommerce", "session-1", Password, "198.51.100.10");

        Assert.NotEmpty(unlock.Token);
    }

    [Fact]
    public void RotatingSessionIdsStillHitsSourceLimiterUntilWindowExpires()
    {
        var now = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        var gate = new ProductionMutationGate(
            Password,
            NullLogger<ProductionMutationGate>.Instance,
            () => now);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Throws<ProductionUnlockFailedException>(() => gate.Unlock(
                "upc_ecommerce", $"rotated-session-{attempt}", "wrong-password", "198.51.100.11"));
        }

        Assert.Throws<ProductionUnlockFailedException>(() => gate.Unlock(
            "upc_ecommerce", "fresh-session", Password, "198.51.100.11"));

        now = now.AddMinutes(1).AddSeconds(1);
        var unlock = gate.Unlock(
            "upc_ecommerce", "fresh-session", Password, "198.51.100.11");

        Assert.NotEmpty(unlock.Token);
    }

    [Fact]
    public void UnknownSourceUsesOneSharedSourcePartition()
    {
        var now = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        var gate = new ProductionMutationGate(
            Password,
            NullLogger<ProductionMutationGate>.Instance,
            () => now);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Throws<ProductionUnlockFailedException>(() => gate.Unlock(
                "upc_ecommerce", $"unknown-source-session-{attempt}", "wrong-password", null));
        }

        Assert.Throws<ProductionUnlockFailedException>(() => gate.Unlock(
            "upc_ecommerce", "unknown-source-fresh-session", Password, null));

        now = now.AddMinutes(1).AddSeconds(1);
        Assert.NotEmpty(gate.Unlock(
            "upc_ecommerce", "unknown-source-fresh-session", Password, null).Token);
    }

    [Fact]
    public void ModuleCeilingIsBoundedAndExpiresForLegitimateRecovery()
    {
        var now = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        var gate = new ProductionMutationGate(
            Password,
            NullLogger<ProductionMutationGate>.Instance,
            () => now);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            Assert.Throws<ProductionUnlockFailedException>(() => gate.Unlock(
                "upc_ecommerce",
                $"module-session-{attempt}",
                "wrong-password",
                $"198.51.100.{attempt + 1}"));
        }

        Assert.InRange(gate.TrackedAttemptCount, 1, 41);
        Assert.Throws<ProductionUnlockFailedException>(() => gate.Unlock(
            "upc_ecommerce", "module-fresh-session", Password, "203.0.113.10"));

        now = now.AddMinutes(1).AddSeconds(1);
        var unlock = gate.Unlock(
            "upc_ecommerce", "module-fresh-session", Password, "203.0.113.10");
        Assert.NotEmpty(unlock.Token);
    }

    [Fact]
    public void TokenCannotCrossModuleBoundary()
    {
        var gate = new ProductionMutationGate(Password, NullLogger<ProductionMutationGate>.Instance);
        var unlock = gate.Unlock("upc_ecommerce", "session-1", Password);

        var error = Assert.Throws<ProductionMutationLockedException>(() => gate.RequireUnlocked(
            Mock.Of<IOrderModule>(module => module.Key == "ghc_ecommerce"),
            ProductionEnvironment("GHC Production"),
            unlock.Token,
            "session-1",
            "send"));

        Assert.Equal("production_mutation_locked", error.Code);
    }

    [Fact]
    public void AttemptAndTokenCachesAreBoundedAndTokenEntriesSelfEvict()
    {
        var gate = new ProductionMutationGate(
            Password,
            NullLogger<ProductionMutationGate>.Instance,
            utcNow: null,
            tokenLifetime: TimeSpan.FromMilliseconds(100),
            attemptWindow: TimeSpan.FromMilliseconds(100));

        Assert.Throws<ProductionUnlockFailedException>(() => gate.Unlock(
            "upc_ecommerce", "session-1", "wrong-password", "198.51.100.12"));
        Assert.InRange(gate.TrackedAttemptCount, 1, 3);

        var unlock = gate.Unlock(
            "ghc_ecommerce", "session-2", Password, "198.51.100.12");
        Assert.Equal(1, gate.TrackedTokenCount);

        Thread.Sleep(250);
        var expirationError = Assert.ThrowsAny<ApiException>(() => gate.RequireUnlocked(
            Mock.Of<IOrderModule>(m => m.Key == "ghc_ecommerce"),
            ProductionEnvironment("GHC Production"),
            unlock.Token,
            "session-2",
            "send"));
        Assert.Contains(expirationError.Code, new[] { "production_mutation_locked", "production_unlock_expired" });
        Assert.Equal(0, gate.TrackedTokenCount);
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
