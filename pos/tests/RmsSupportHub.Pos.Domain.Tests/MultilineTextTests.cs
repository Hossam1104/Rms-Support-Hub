using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Tests;

public sealed class MultilineTextTests
{
    [Fact]
    public void SplitLinesHandlesMixedEditorLineEndings()
    {
        var lines = MultilineText.SplitLines("RmsCashierSrv\nRmsBranchSrv\r\n RMSArchive \r\n");

        Assert.Equal(["RmsCashierSrv", "RmsBranchSrv", "RMSArchive"], lines);
    }
}
