using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Configuration;
using RmsSupportHub.Pos.Infrastructure.Packages;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class AgentPackageMachineTrustCompositionTests
{
    private const string ProductionPin = "1111111111111111111111111111111111111111";
    private const string TestingPin = "2222222222222222222222222222222222222222";

    [Fact]
    public void ObsoletePosAgentReleaseChannelConfigurationFailsStartupClosed()
    {
        using var fixture = new TestTrustFixture("Production");
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using var baseFactory = new AgentWebApplicationFactory();
            baseFactory.UseTestTrustConfiguration(fixture.TrustFilePath);
            using var factory = baseFactory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("PosAgent:ReleaseChannel", "Testing");
            });
            _ = factory.Services;
        });

        Assert.Contains("PosAgent:ReleaseChannel is obsolete and rejected", ex.Message);
    }

    [Fact]
    public void ConflictingProcessConfigurationCannotAlterMachineTrustMode()
    {
        using var fixture = new TestTrustFixture("Production");
        // Injecting PosAgent:ReleaseChannel=Testing when machine trust says Production fails startup
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using var baseFactory = new AgentWebApplicationFactory();
            baseFactory.UseTestTrustConfiguration(fixture.TrustFilePath);
            using var factory = baseFactory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("PosAgent:ReleaseChannel", "Testing");
            });
            _ = factory.Services;
        });

        Assert.Contains("obsolete and rejected", ex.Message);
    }

    [Theory]
    [InlineData("PosAgent:ReleaseChannel")]
    [InlineData("PosAgent:TrustConfigurationPath")]
    public void ObsoletePosAgentConfigurationPresenceFailsStartupEvenWhenEmpty(string key)
    {
        using var fixture = new TestTrustFixture("Production");
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using var baseFactory = new AgentWebApplicationFactory();
            baseFactory.UseTestTrustConfiguration(fixture.TrustFilePath);
            using var factory = baseFactory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting(key, string.Empty);
            });
            _ = factory.Services;
        });

        Assert.Contains($"{key} is obsolete and rejected", ex.Message);
    }

    [Fact]
    public void EnvironmentTrustPathOverrideIsRejectedByPresence()
    {
        using var fixture = new TestTrustFixture("Production");
        var alternatePathKey = "PosAgent__TrustConfigurationPath";
        var previous = Environment.GetEnvironmentVariable(alternatePathKey);
        try
        {
            Environment.SetEnvironmentVariable(alternatePathKey, fixture.TrustFilePath);
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                using var baseFactory = new AgentWebApplicationFactory();
                baseFactory.UseTestTrustConfiguration(fixture.TrustFilePath);
                using var factory = baseFactory.WithWebHostBuilder(_ => { });
                _ = factory.Services;
            });

            Assert.Contains("PosAgent:TrustConfigurationPath is obsolete and rejected", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(alternatePathKey, previous);
        }
    }

    [Fact]
    public void MachineTrustSnapshotDeterminesLifecycleReleaseChannelIndependentlyOfEnvironment()
    {
        using var fixture = new TestTrustFixture("Production");
        // Using "IntegrationTest" host environment, but machine trust says "Production"
        using var baseFactory = new AgentWebApplicationFactory();
        baseFactory.UseTestTrustConfiguration(fixture.TrustFilePath);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
        });

        var packageOptions = factory.Services.GetRequiredService<AgentPackageOptions>();
        var trustSnapshot = factory.Services.GetRequiredService<AgentMachineTrustConfiguration>();

        Assert.Equal("Production", trustSnapshot.DeploymentMode);
        Assert.Equal("Production", packageOptions.ReleaseChannel);
        Assert.Equal(ProductionPin, trustSnapshot.ProductionSignerThumbprint);
    }

    [Fact]
    public void TestingMachineTrustSelectsTestingReleaseChannel()
    {
        using var fixture = new TestTrustFixture("Testing");
        using var baseFactory = new AgentWebApplicationFactory();
        baseFactory.UseTestTrustConfiguration(fixture.TrustFilePath);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
        });

        var packageOptions = factory.Services.GetRequiredService<AgentPackageOptions>();
        var trustSnapshot = factory.Services.GetRequiredService<AgentMachineTrustConfiguration>();

        Assert.Equal("Testing", trustSnapshot.DeploymentMode);
        Assert.Equal("Testing", packageOptions.ReleaseChannel);
        Assert.Equal(TestingPin, trustSnapshot.TestingSignerThumbprint);
    }

    [Fact]
    public void MissingMachineTrustConfigurationFailsStartupClosed()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using var factory = new AgentWebApplicationFactory(
                AgentHostConstants.IntegrationTestEnvironment,
                useTestTrust: false);
            _ = factory.Services;
        });

        Assert.Contains("machine trust configuration could not be loaded", ex.Message);
    }

    private sealed class TestTrustFixture : IDisposable
    {
        public string TestRoot { get; }
        public string TrustFilePath { get; }

        public TestTrustFixture(string deploymentMode)
        {
            TestRoot = Path.Combine(Path.GetTempPath(), "RmsSupportHub.Pos.Tests", "comp-trust-" + Guid.NewGuid().ToString("N"));
            var trustDir = Path.Combine(TestRoot, "Trust");
            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(trustDir);
            TrustFilePath = Path.Combine(trustDir, "package-trust.json");

            var json = $$"""
            {
              "productionSignerThumbprint": "{{ProductionPin}}",
              "testingSignerThumbprint": "{{TestingPin}}",
              "deploymentMode": "{{deploymentMode}}"
            }
            """;

            File.WriteAllText(TrustFilePath, json);
            ServiceOwnedDirectoryProvisioner.EnsureControlFileAcl(TrustFilePath);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(TestRoot)) Directory.Delete(TestRoot, recursive: true);
            }
            catch
            {
            }
        }
    }
}
