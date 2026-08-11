using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Tests;

public sealed class AgentConfigurationTests
{
    [Fact]
    public void FreshInstance_ContainsNoCredentialOrEnvironmentSpecificAddress()
    {
        var config = new AgentConfiguration();

        Assert.Equal(string.Empty, config.SqlInstance);
        Assert.Equal(string.Empty, config.SqlUser);
        Assert.Equal(string.Empty, config.ApiBaseUrl);
        Assert.Equal(string.Empty, config.BackupFolder);
        Assert.Empty(config.Databases);
        Assert.Empty(config.Services);
        Assert.Equal(string.Empty, config.Downloader.ApiUrl);
        Assert.Equal(string.Empty, config.Downloader.RdbServerIp);
        Assert.Equal(string.Empty, config.Downloader.RdbUsername);
        Assert.Empty(config.Downloader.KnownBranchCodes);
    }

    [Fact]
    public void NoPublicPropertyOnConfigurationOrDownloaderCarriesAPassword()
    {
        var offenders = new[] { typeof(AgentConfiguration), typeof(AgentDownloaderConfiguration) }
            .SelectMany(t => t.GetProperties())
            .Where(p => p.Name.Contains("password", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Clone_ProducesAnIndependentCopy()
    {
        var original = new AgentConfiguration
        {
            SqlInstance = "SQLEXPRESS",
            Databases = ["db1"],
            Version = 3,
        };
        original.Downloader.KnownBranchCodes.Add("P001");

        var clone = original.Clone();
        clone.Databases.Add("db2");
        clone.Downloader.KnownBranchCodes.Add("P002");

        Assert.Single(original.Databases);
        Assert.Single(original.Downloader.KnownBranchCodes);
        Assert.Equal(2, clone.Databases.Count);
        Assert.Equal(2, clone.Downloader.KnownBranchCodes.Count);
        Assert.Equal(original.SqlInstance, clone.SqlInstance);
        Assert.Equal(original.Version, clone.Version);
    }
}
