using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Tests;

public sealed class DatabaseResolverTests
{
    [Fact]
    public void ResolveBranchDatabasePrefersBranchNamedDatabase()
    {
        var settings = new AppSettings
        {
            Databases = ["RmsCashierSrv", "RmsBranchSrv", "OtherDb"]
        };

        Assert.Equal("RmsBranchSrv", DatabaseResolver.ResolveBranchDatabase(settings));
    }

    [Fact]
    public void ResolvePrimaryDatabaseUsesBranchDatabase()
    {
        var settings = new AppSettings
        {
            Databases = ["RmsCashierSrv", "CustomBranchDb"]
        };

        Assert.Equal("CustomBranchDb", DatabaseResolver.ResolvePrimaryDatabase(settings));
    }
}
