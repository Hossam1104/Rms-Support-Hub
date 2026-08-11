using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Tests;

public sealed class OperationResultTests
{
    [Fact]
    public void FinalizeSuccessMarksResultSuccessful()
    {
        var result = OperationResult.Running("backup_database");

        result.AddMessage("done");
        result.Finalize(OperationStatus.Success);

        Assert.True(result.Success);
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotNull(result.EndTime);
        Assert.Contains("done", result.Messages);
    }
}
