using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RmsSupportHub.Api.Configuration;
using RmsSupportHub.Core.Modules;
using Xunit;

namespace RmsSupportHub.Tests;

[Collection("HostEnvironmentCollection")]
public sealed class ExternalConfigurationLoaderTests : IDisposable
{
    private static readonly object EnvLock = new();
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirectories = new();

    public void Dispose()
    {
        lock (EnvLock)
        {
            Environment.SetEnvironmentVariable(ExternalConfigurationLoader.ExternalConfigPathVariableName, null);
            Environment.SetEnvironmentVariable("SupportHub__HealthProbe__TimeoutSeconds", null);
        }

        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { }
            }
        }

        foreach (var dir in _tempDirectories)
        {
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
    }

    private string CreateTempConfigFile(string jsonContent)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "rmshub_test_extconfig_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);

        var filePath = Path.Combine(tempDir, "appsettings.override.json");
        File.WriteAllText(filePath, jsonContent);
        _tempFiles.Add(filePath);
        return filePath;
    }

    [Fact]
    public void ValidateAndNormalize_WhenNullOrEmpty_ReturnsNull()
    {
        var contentRoot = Path.GetTempPath();
        Assert.Null(ExternalConfigurationLoader.ValidateAndNormalizeExternalConfigPath(null, contentRoot));
        Assert.Null(ExternalConfigurationLoader.ValidateAndNormalizeExternalConfigPath(string.Empty, contentRoot));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void ValidateAndNormalize_WhenWhitespace_Throws(string whitespace)
    {
        var contentRoot = Path.GetTempPath();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ExternalConfigurationLoader.ValidateAndNormalizeExternalConfigPath(whitespace, contentRoot));
        Assert.Contains("whitespace", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http://localhost/appsettings.json")]
    [InlineData("https://example.com/appsettings.json")]
    [InlineData("ftp://fileserver/appsettings.json")]
    [InlineData("file:///C:/config.json")]
    public void ValidateAndNormalize_WhenUrlScheme_Throws(string url)
    {
        var contentRoot = Path.GetTempPath();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ExternalConfigurationLoader.ValidateAndNormalizeExternalConfigPath(url, contentRoot));
        Assert.Contains("URL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(@"\\server\share\appsettings.override.json")]
    [InlineData(@"//server/share/appsettings.override.json")]
    [InlineData(@"\\?\UNC\server\share\appsettings.override.json")]
    public void ValidateAndNormalize_WhenUncPath_Throws(string unc)
    {
        var contentRoot = Path.GetTempPath();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ExternalConfigurationLoader.ValidateAndNormalizeExternalConfigPath(unc, contentRoot));
        Assert.Contains("UNC", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("appsettings.override.json")]
    [InlineData("./appsettings.override.json")]
    [InlineData("../appsettings.override.json")]
    [InlineData("subfolder/appsettings.override.json")]
    public void ValidateAndNormalize_WhenRelativePath_Throws(string relativePath)
    {
        var contentRoot = Path.GetTempPath();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ExternalConfigurationLoader.ValidateAndNormalizeExternalConfigPath(relativePath, contentRoot));
        Assert.Contains("absolute", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndNormalize_WhenInsideContentRoot_Throws()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "mock_app_root_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        _tempDirectories.Add(contentRoot);

        var insidePath = Path.Combine(contentRoot, "appsettings.override.json");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ExternalConfigurationLoader.ValidateAndNormalizeExternalConfigPath(insidePath, contentRoot));
        Assert.Contains("content root", ex.Message, StringComparison.OrdinalIgnoreCase);

        var subDirInsidePath = Path.Combine(contentRoot, "config", "appsettings.override.json");
        var ex2 = Assert.Throws<InvalidOperationException>(() =>
            ExternalConfigurationLoader.ValidateAndNormalizeExternalConfigPath(subDirInsidePath, contentRoot));
        Assert.Contains("content root", ex2.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateJsonFile_WhenMissingFile_ThrowsFileNotFound()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "nonexistent_" + Guid.NewGuid().ToString("N") + ".json");
        Assert.Throws<FileNotFoundException>(() => ExternalConfigurationLoader.ValidateJsonFile(nonExistentPath));
    }

    [Fact]
    public void ValidateJsonFile_WhenMalformedJson_ThrowsSafelyWithoutSecretDisclosure()
    {
        const string secretValue = "SuperSecretDatabasePassword999!";
        var malformedJson = $"{{ \"Secret\": \"{secretValue}\", \"Malformed\": ";
        var filePath = CreateTempConfigFile(malformedJson);

        var ex = Assert.Throws<InvalidOperationException>(() => ExternalConfigurationLoader.ValidateJsonFile(filePath));
        Assert.Contains("invalid JSON", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secretValue, ex.Message);
    }

    [Fact]
    public void ValidateJsonFile_WhenRootIsNotObject_Throws()
    {
        var arrayJson = "[1, 2, 3]";
        var filePath = CreateTempConfigFile(arrayJson);

        var ex = Assert.Throws<InvalidOperationException>(() => ExternalConfigurationLoader.ValidateJsonFile(filePath));
        Assert.Contains("object", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HostStartup_WhenVariableAbsent_PreservesPackagedDefaults()
    {
        lock (EnvLock)
        {
            Environment.SetEnvironmentVariable(ExternalConfigurationLoader.ExternalConfigPathVariableName, null);

            using var factory = new TestHostApplicationFactory();
            using var client = factory.CreateClient();

            var policy = factory.Services.GetRequiredService<IEnvironmentPolicy>();
            Assert.Equal(DeploymentTier.Testing, policy.DeploymentTier);

            var config = factory.Services.GetRequiredService<IConfiguration>();
            Assert.Equal("Testing", config["SupportHub:DeploymentTier"]);
        }
    }

    [Fact]
    public void HostStartup_WhenValidExternalJsonProvided_OverridesPackagedDefaults()
    {
        lock (EnvLock)
        {
            var externalJson = JsonSerializer.Serialize(new
            {
                SupportHub = new
                {
                    DeploymentTier = "Testing",
                    HealthProbe = new
                    {
                        Enabled = false,
                        TimeoutSeconds = 7
                    }
                },
                ConnectionStrings = new
                {
                    UpcEcommerceTest = "Server=127.0.0.1;Database=CustomExternalDb;Integrated Security=True;TrustServerCertificate=True;"
                }
            });

            var filePath = CreateTempConfigFile(externalJson);
            Environment.SetEnvironmentVariable(ExternalConfigurationLoader.ExternalConfigPathVariableName, filePath);

            using var factory = new TestHostApplicationFactory();
            using var client = factory.CreateClient();

            var options = factory.Services.GetRequiredService<IOptions<SupportHubOptions>>().Value;
            Assert.False(options.HealthProbe.Enabled);
            Assert.Equal(7, options.HealthProbe.TimeoutSeconds);

            var config = factory.Services.GetRequiredService<IConfiguration>();
            Assert.Equal("Server=127.0.0.1;Database=CustomExternalDb;Integrated Security=True;TrustServerCertificate=True;",
                config["ConnectionStrings:UpcEcommerceTest"]);
        }
    }

    [Fact]
    public void Precedence_EnvironmentVariable_OverridesExternalJson()
    {
        lock (EnvLock)
        {
            var externalJson = JsonSerializer.Serialize(new
            {
                SupportHub = new
                {
                    DeploymentTier = "Testing",
                    HealthProbe = new
                    {
                        Enabled = false,
                        TimeoutSeconds = 12
                    }
                }
            });

            var filePath = CreateTempConfigFile(externalJson);
            Environment.SetEnvironmentVariable(ExternalConfigurationLoader.ExternalConfigPathVariableName, filePath);
            Environment.SetEnvironmentVariable("SupportHub__HealthProbe__TimeoutSeconds", "18");

            try
            {
                using var factory = new TestHostApplicationFactory();
                using var client = factory.CreateClient();

                var options = factory.Services.GetRequiredService<IOptions<SupportHubOptions>>().Value;
                // Environment variable (18) should override external JSON (12)
                Assert.Equal(18, options.HealthProbe.TimeoutSeconds);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SupportHub__HealthProbe__TimeoutSeconds", null);
            }
        }
    }

    [Fact]
    public void Precedence_CommandLine_OverridesEnvironmentVariableAndExternalJson()
    {
        lock (EnvLock)
        {
            var externalJson = JsonSerializer.Serialize(new
            {
                SupportHub = new
                {
                    DeploymentTier = "Testing",
                    HealthProbe = new
                    {
                        Enabled = false,
                        TimeoutSeconds = 10
                    }
                }
            });

            var filePath = CreateTempConfigFile(externalJson);
            Environment.SetEnvironmentVariable(ExternalConfigurationLoader.ExternalConfigPathVariableName, filePath);
            Environment.SetEnvironmentVariable("SupportHub__HealthProbe__TimeoutSeconds", "20");

            try
            {
                using var factory = new TestHostApplicationFactory(new[] { "--SupportHub:HealthProbe:TimeoutSeconds=25" });
                using var client = factory.CreateClient();

                var options = factory.Services.GetRequiredService<IOptions<SupportHubOptions>>().Value;
                // Command-line (25) should override environment variable (20) and external JSON (10)
                Assert.Equal(25, options.HealthProbe.TimeoutSeconds);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SupportHub__HealthProbe__TimeoutSeconds", null);
            }
        }
    }

    [Fact]
    public void HostStartup_WhenConfiguredMissingFile_FailsStartup()
    {
        lock (EnvLock)
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), "missing_ext_cfg_" + Guid.NewGuid().ToString("N") + ".json");
            Environment.SetEnvironmentVariable(ExternalConfigurationLoader.ExternalConfigPathVariableName, nonExistentPath);

            using var factory = new TestHostApplicationFactory();
            var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

            var messages = CollectMessages(ex);
            Assert.Contains("was not found", messages, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void HostStartup_WhenMalformedJson_FailsStartupSafely()
    {
        lock (EnvLock)
        {
            const string sensitiveSecret = "SuperSecretConnectionStringPassword_XYZ!";
            var malformedJson = $"{{ \"Secret\": \"{sensitiveSecret}\", \"BadJson\": ";
            var filePath = CreateTempConfigFile(malformedJson);
            Environment.SetEnvironmentVariable(ExternalConfigurationLoader.ExternalConfigPathVariableName, filePath);

            using var factory = new TestHostApplicationFactory();
            var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

            var messages = CollectMessages(ex);
            Assert.Contains("invalid JSON", messages, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(sensitiveSecret, messages);
        }
    }

    [Theory]
    [InlineData("http://evil.example.invalid/appsettings.json")]
    [InlineData("https://evil.example.invalid/appsettings.json")]
    [InlineData(@"\\evilsrv\share\appsettings.json")]
    [InlineData("relative/path/appsettings.json")]
    public void HostStartup_WhenInvalidPathConfigured_FailsStartup(string invalidPath)
    {
        lock (EnvLock)
        {
            Environment.SetEnvironmentVariable(ExternalConfigurationLoader.ExternalConfigPathVariableName, invalidPath);

            using var factory = new TestHostApplicationFactory();
            var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

            var messages = CollectMessages(ex);
            Assert.True(
                messages.Contains("URL", StringComparison.OrdinalIgnoreCase) ||
                messages.Contains("UNC", StringComparison.OrdinalIgnoreCase) ||
                messages.Contains("absolute", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string CollectMessages(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }
        return string.Join(" | ", messages);
    }

    private sealed class TestHostApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string[]? _commandLineArgs;

        public TestHostApplicationFactory(string[]? commandLineArgs = null)
        {
            _commandLineArgs = commandLineArgs;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            if (_commandLineArgs is { Length: > 0 })
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddCommandLine(_commandLineArgs);
                });
            }
        }
    }
}
