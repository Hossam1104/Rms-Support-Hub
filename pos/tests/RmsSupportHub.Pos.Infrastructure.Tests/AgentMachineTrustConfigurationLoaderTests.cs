using System.Security.AccessControl;
using System.Security.Principal;
using RmsSupportHub.Pos.Application.Packages;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Configuration;
using RmsSupportHub.Pos.Infrastructure.Packages;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

public sealed class AgentMachineTrustConfigurationLoaderTests
{
    private const string ProductionPin = "1111111111111111111111111111111111111111";
    private const string TestingPin = "2222222222222222222222222222222222222222";

    [Fact]
    public void ValidProductionMachineTrustSelectsProduction()
    {
        using var fixture = new MachineTrustTestFixture(
            deploymentMode: "Production",
            productionPin: ProductionPin,
            testingPin: TestingPin);

        var loader = new MachineAgentTrustConfigurationLoader();
        var result = loader.Load(fixture.TrustFilePath);

        Assert.NotNull(result);
        Assert.Equal("Production", result.DeploymentMode);
        Assert.Equal(ProductionPin, result.ProductionSignerThumbprint);
        Assert.Equal(TestingPin, result.TestingSignerThumbprint);
        Assert.Equal(ProductionPin, result.ActiveSignerThumbprint);
        Assert.Equal(ProductionPin, result.GetConfiguredThumbprint("Production"));
        Assert.Equal(TestingPin, result.GetConfiguredThumbprint("Testing"));
    }

    [Fact]
    public void ValidTestingMachineTrustSelectsTesting()
    {
        using var fixture = new MachineTrustTestFixture(
            deploymentMode: "Testing",
            productionPin: ProductionPin,
            testingPin: TestingPin);

        var loader = new MachineAgentTrustConfigurationLoader();
        var result = loader.Load(fixture.TrustFilePath);

        Assert.NotNull(result);
        Assert.Equal("Testing", result.DeploymentMode);
        Assert.Equal(ProductionPin, result.ProductionSignerThumbprint);
        Assert.Equal(TestingPin, result.TestingSignerThumbprint);
        Assert.Equal(TestingPin, result.ActiveSignerThumbprint);
    }

    [Fact]
    public void MissingDeploymentModeIsRejected()
    {
        using var fixture = new MachineTrustTestFixture(
            customJson: $$"""
            {
              "productionSignerThumbprint": "{{ProductionPin}}",
              "testingSignerThumbprint": "{{TestingPin}}"
            }
            """);

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("machine_release_mode_missing", failureCode);
        Assert.Throws<InvalidOperationException>(() => loader.Load(fixture.TrustFilePath));
    }

    [Fact]
    public void EmptyDeploymentModeIsRejected()
    {
        using var fixture = new MachineTrustTestFixture(
            customJson: $$"""
            {
              "productionSignerThumbprint": "{{ProductionPin}}",
              "testingSignerThumbprint": "{{TestingPin}}",
              "deploymentMode": ""
            }
            """);

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("machine_release_mode_invalid", failureCode);
    }

    [Theory]
    [InlineData("InvalidMode")]
    [InlineData("Dev")]
    [InlineData("Staging")]
    [InlineData("123")]
    public void MalformedDeploymentModeIsRejected(string invalidMode)
    {
        using var fixture = new MachineTrustTestFixture(
            customJson: $$"""
            {
              "productionSignerThumbprint": "{{ProductionPin}}",
              "testingSignerThumbprint": "{{TestingPin}}",
              "deploymentMode": "{{invalidMode}}"
            }
            """);

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("machine_release_mode_invalid", failureCode);
    }

    [Theory]
    [InlineData("production")]
    [InlineData("testing")]
    [InlineData("PRODUCTION")]
    [InlineData("TESTING")]
    [InlineData(" Production")]
    [InlineData("Testing ")]
    public void LowercaseOrAlternateSpellingDeploymentModeIsRejected(string alternateMode)
    {
        using var fixture = new MachineTrustTestFixture(
            customJson: $$"""
            {
              "productionSignerThumbprint": "{{ProductionPin}}",
              "testingSignerThumbprint": "{{TestingPin}}",
              "deploymentMode": "{{alternateMode}}"
            }
            """);

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("machine_release_mode_invalid", failureCode);
    }

    [Fact]
    public void ProductionModeWithoutProductionSignerPinIsRejected()
    {
        using var fixture = new MachineTrustTestFixture(
            customJson: $$"""
            {
              "testingSignerThumbprint": "{{TestingPin}}",
              "deploymentMode": "Production"
            }
            """);

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("signer_thumbprint_missing", failureCode);
    }

    [Fact]
    public void TestingModeWithoutTestingSignerPinIsRejected()
    {
        using var fixture = new MachineTrustTestFixture(
            customJson: $$"""
            {
              "productionSignerThumbprint": "{{ProductionPin}}",
              "deploymentMode": "Testing"
            }
            """);

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("signer_thumbprint_missing", failureCode);
    }

    [Theory]
    [InlineData("", TestingPin)]
    [InlineData("   ", TestingPin)]
    [InlineData(ProductionPin, "")]
    [InlineData(ProductionPin, "   ")]
    public void EmptyOrWhitespaceSignerPinsAreRejected(string productionPin, string testingPin)
    {
        using var fixture = new MachineTrustTestFixture(
            customJson: $$"""
            {
              "productionSignerThumbprint": "{{productionPin}}",
              "testingSignerThumbprint": "{{testingPin}}",
              "deploymentMode": "Testing"
            }
            """);

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("signer_thumbprint_invalid", failureCode);
    }

    [Fact]
    public void NonStringSignerPinIsRejected()
    {
        using var fixture = new MachineTrustTestFixture(
            customJson: $$"""
            {
              "productionSignerThumbprint": 123,
              "testingSignerThumbprint": "{{TestingPin}}",
              "deploymentMode": "Testing"
            }
            """);

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("signer_thumbprint_invalid", failureCode);
    }

    [Fact]
    public void CanonicalTrustPathIsExactlyTheProgramDataAuthority()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "DBS",
            "RmsSupportAgent",
            "Trust",
            "package-trust.json");

        Assert.Equal(expected, MachineAgentTrustConfigurationLoader.CanonicalTrustConfigurationPath);
    }

    [Fact]
    public void EqualSignerPinsAreRejected()
    {
        using var fixture = new MachineTrustTestFixture(
            customJson: $$"""
            {
              "productionSignerThumbprint": "{{ProductionPin}}",
              "testingSignerThumbprint": "{{ProductionPin}}",
              "deploymentMode": "Production"
            }
            """);

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("signer_pins_equal", failureCode);
    }

    [Theory]
    [InlineData("aabbccddee11223344556677889900aabbccddee", "AABBCCDDEE11223344556677889900AABBCCDDEE")]
    [InlineData("AABBCCDDEE11223344556677889900AABBCCDDEE", " aabb ccdd ee11 2233 4455 6677 8899 00aa bbcc ddee ")]
    public void SignerEqualityDifferingOnlyByCaseOrWhitespaceIsRejected(string prod, string test)
    {
        using var fixture = new MachineTrustTestFixture(
            customJson: $$"""
            {
              "productionSignerThumbprint": "{{prod}}",
              "testingSignerThumbprint": "{{test}}",
              "deploymentMode": "Production"
            }
            """);

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("signer_pins_equal", failureCode);
    }

    [Theory]
    [InlineData("not-a-hex-string")]
    [InlineData("1122334455")]
    [InlineData("111111111111111111111111111111111111111G")]
    public void MalformedSignerThumbprintIsRejected(string malformedPin)
    {
        using var fixture = new MachineTrustTestFixture(
            customJson: $$"""
            {
              "productionSignerThumbprint": "{{malformedPin}}",
              "testingSignerThumbprint": "{{TestingPin}}",
              "deploymentMode": "Testing"
            }
            """);

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("signer_thumbprint_invalid", failureCode);
    }

    [Fact]
    public void UnsafeTrustControlAclIsRejected()
    {
        using var fixture = new MachineTrustTestFixture(
            deploymentMode: "Production",
            productionPin: ProductionPin,
            testingPin: TestingPin);

        // Grant WorldSid (Everyone) read access, making ACL unsafe
        var security = new FileInfo(fixture.TrustFilePath).GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            FileSystemRights.Read,
            AccessControlType.Allow));
        new FileInfo(fixture.TrustFilePath).SetAccessControl(security);

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("machine_trust_untrusted_acl", failureCode);
    }

    [Fact]
    public void ReparseControlFileIsRejected()
    {
        var outsideRoot = Path.Combine(Path.GetTempPath(), "rms-reparse-outside-" + Guid.NewGuid().ToString("N"));
        var testRoot = Path.Combine(Path.GetTempPath(), "RmsSupportHub.Pos.Tests", "rms-reparse-test-" + Guid.NewGuid().ToString("N"));
        var junctionDir = Path.Combine(testRoot, "JunctionTrust");

        try
        {
            Directory.CreateDirectory(outsideRoot);
            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(testRoot);

            var realFile = Path.Combine(outsideRoot, "package-trust.json");
            File.WriteAllText(realFile, $$"""
            {
              "productionSignerThumbprint": "{{ProductionPin}}",
              "deploymentMode": "Production"
            }
            """);

            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{junctionDir}\" \"{outsideRoot}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            process?.WaitForExit();

            var junctionFile = Path.Combine(junctionDir, "package-trust.json");
            if (File.Exists(junctionFile))
            {
                var loader = new MachineAgentTrustConfigurationLoader();
                var success = loader.TryLoad(junctionFile, out var config, out var failureCode);

                Assert.False(success);
                Assert.Null(config);
                Assert.True(failureCode is "machine_trust_reparse_point" or "machine_trust_untrusted_acl");
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(junctionDir)) Directory.Delete(junctionDir);
            }
            catch
            {
            }

            try
            {
                if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
            }
            catch
            {
            }

            try
            {
                if (Directory.Exists(outsideRoot)) Directory.Delete(outsideRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void MalformedJsonIsRejected()
    {
        using var fixture = new MachineTrustTestFixture(customJson: "{ not valid json ");

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("machine_trust_json_invalid", failureCode);
    }

    [Fact]
    public void NonObjectJsonIsRejected()
    {
        using var fixture = new MachineTrustTestFixture(customJson: "[\"Production\"]");

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("machine_trust_json_invalid", failureCode);
    }

    [Fact]
    public void OversizedFileIsRejected()
    {
        var padding = new string(' ', MachineAgentTrustConfigurationLoader.MaxControlFileBytes + 100);
        using var fixture = new MachineTrustTestFixture(
            customJson: $$"""
            {
              "productionSignerThumbprint": "{{ProductionPin}}",
              "deploymentMode": "Production",
              "padding": "{{padding}}"
            }
            """);

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(fixture.TrustFilePath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.True(failureCode is "machine_trust_file_size_invalid" or "machine_trust_untrusted_acl");
    }

    [Fact]
    public void MissingFileIsRejected()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            "RmsSupportHub.Pos.Tests",
            "non-existent-" + Guid.NewGuid().ToString("N"),
            "package-trust.json");

        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(missingPath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("machine_trust_file_missing", failureCode);
    }

    [Theory]
    [InlineData("relative/package-trust.json")]
    [InlineData("..\\package-trust.json")]
    [InlineData("C:\\invalid\0\\package-trust.json")]
    public void InvalidPathFormatIsRejected(string invalidPath)
    {
        var loader = new MachineAgentTrustConfigurationLoader();
        var success = loader.TryLoad(invalidPath, out var config, out var failureCode);

        Assert.False(success);
        Assert.Null(config);
        Assert.Equal("machine_trust_path_invalid", failureCode);
    }

    [Fact]
    public async Task PackageChannelMismatchWithMachineModeIsRejectedByVerifier()
    {
        using var fixture = new MachineTrustTestFixture(
            deploymentMode: "Production",
            productionPin: ProductionPin,
            testingPin: TestingPin);

        var trust = new MachineAgentTrustConfigurationLoader().Load(fixture.TrustFilePath);
        var packageOptions = new AgentPackageOptions
        {
            ReleaseChannel = trust.DeploymentMode,
            PackageRoot = Path.Combine(fixture.TestRoot, "packages"),
            InstallationRoot = Path.Combine(fixture.TestRoot, "installation")
        };

        var verifier = new FileAgentPackageVerifier(
            packageOptions,
            new AgentPackagePolicy(),
            new NoopSignatureVerifier());

        var testingManifest = new AgentPackageManifest(
            "rms-support-agent",
            "1.0.0",
            "Windows",
            "net10.0-windows",
            AgentProductIdentity.ServiceDisplayName,
            "LocalSystem",
            AgentProductIdentity.PermanentServiceName,
            "SHA256withRSA",
            "DBS test signer",
            new string('a', 64),
            "signature",
            [new AgentPackageFileManifest("agent", "RmsSupportAgent.exe", 1, new string('b', 64), true)],
            ["AgentServiceReadWrite"],
            ["AgentHttpsCertificate"],
            null,
            false,
            1,
            1,
            AgentProductIdentity.ProductId,
            "x64",
            AgentProductIdentity.ReleaseChannelTesting,
            AgentProductIdentity.EnvironmentTesting,
            AgentProductIdentity.ServiceDescription);

        var result = await verifier.VerifyAsync(testingManifest);

        Assert.Equal(AgentPackageVerificationState.Rejected, result.State);
        Assert.Contains("package_channel_not_configured", result.Blockers);
    }

    private sealed class NoopSignatureVerifier : IAgentPackageSignatureVerifier
    {
        public Task<bool> VerifyAsync(AgentPackageManifest manifest, string archivePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class MachineTrustTestFixture : IDisposable
    {
        public string TestRoot { get; }
        public string TrustFilePath { get; }

        public MachineTrustTestFixture(
            string? deploymentMode = null,
            string? productionPin = null,
            string? testingPin = null,
            string? customJson = null)
        {
            TestRoot = Path.Combine(Path.GetTempPath(), "RmsSupportHub.Pos.Tests", "trust-test-" + Guid.NewGuid().ToString("N"));
            var trustDir = Path.Combine(TestRoot, "Trust");
            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(trustDir);
            TrustFilePath = Path.Combine(trustDir, "package-trust.json");

            var content = customJson ?? $$"""
            {
              "productionSignerThumbprint": {{(productionPin is not null ? $"\"{productionPin}\"" : "null")}},
              "testingSignerThumbprint": {{(testingPin is not null ? $"\"{testingPin}\"" : "null")}},
              "deploymentMode": {{(deploymentMode is not null ? $"\"{deploymentMode}\"" : "null")}}
            }
            """;

            File.WriteAllText(TrustFilePath, content);
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
