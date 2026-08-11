using RmsSupportHub.Pos.Application.Restore;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

public sealed class RestoreSqlPlanBuilderTests
{
    [Fact]
    public void Build_MapsAllLogicalFilesToServerOwnedNames()
    {
        var plan = new RestoreSqlPlanBuilder().Build(
            "RmsBranchSrv",
            Path.Combine(Path.GetTempPath(), "restore-plan-tests"),
            [
                new RestoreFileInfo("branch_data", "D"),
                new RestoreFileInfo("branch_data_2", "D"),
                new RestoreFileInfo("branch_log", "L"),
                new RestoreFileInfo("branch_log_2", "L"),
            ]);

        Assert.Equal(
            ["RmsBranchSrv.mdf", "RmsBranchSrv_2.ndf", "RmsBranchSrv_log.ldf", "RmsBranchSrv_log_2.ldf"],
            plan.Moves.Select(move => move.DestinationFileName).ToArray());
        Assert.All(plan.Moves, move => Assert.DoesNotContain(Path.GetTempPath(), move.DestinationFileName, StringComparison.OrdinalIgnoreCase));
        Assert.All(plan.Moves, move => Assert.StartsWith(plan.DbFilesPath, move.DestinationPath, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("..\\outside")]
    [InlineData("%TEMP%\\restore")]
    [InlineData("relative\\restore")]
    public void Build_RejectsUnsafeDestination(string destination)
    {
        var exception = Assert.Throws<RestorePolicyException>(() => new RestoreSqlPlanBuilder().Build(
            "RmsBranchSrv",
            destination,
            [new RestoreFileInfo("branch_data", "D")]));

        Assert.Equal("restore.destination_unsafe", exception.Code);
    }

    [Fact]
    public void Build_RejectsDuplicateOrPathLikeLogicalNames()
    {
        var duplicate = Assert.Throws<RestorePolicyException>(() => new RestoreSqlPlanBuilder().Build(
            "RmsBranchSrv",
            Path.GetTempPath(),
            [new RestoreFileInfo("branch_data", "D"), new RestoreFileInfo("BRANCH_DATA", "D")]));
        Assert.Equal("restore.sql_plan_invalid", duplicate.Code);

        var pathLike = Assert.Throws<RestorePolicyException>(() => new RestoreSqlPlanBuilder().Build(
            "RmsBranchSrv",
            Path.GetTempPath(),
            [new RestoreFileInfo("..\\branch_data", "D")]));
        Assert.Equal("restore.sql_plan_invalid", pathLike.Code);
    }
}
