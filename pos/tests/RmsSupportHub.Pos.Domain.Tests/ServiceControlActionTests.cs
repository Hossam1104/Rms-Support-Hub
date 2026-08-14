using RmsSupportHub.Pos.Domain.Enums;

namespace RmsSupportHub.Pos.Domain.Tests;

public sealed class ServiceControlActionTests
{
    [Fact]
    public void OnlyStartStopRestartAreSupported()
    {
        var values = Enum.GetValues<ServiceControlAction>();

        Assert.Equal(
            new[] { ServiceControlAction.Start, ServiceControlAction.Stop, ServiceControlAction.Restart },
            values);
    }

    [Fact]
    public void NoUnsupportedDeleteLikeValueCanMapToAnOperation()
    {
        Assert.DoesNotContain(
            Enum.GetNames<ServiceControlAction>(),
            name => name.Equals("Delete", StringComparison.OrdinalIgnoreCase));
    }
}
