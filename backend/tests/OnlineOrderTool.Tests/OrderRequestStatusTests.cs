using OnlineOrderTool.Core;
using Xunit;

namespace OnlineOrderTool.Tests;

public class OrderRequestStatusTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(7, true)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(0, false)]
    [InlineData(99, false)]
    public void ResendEligibilityUsesTheCanonicalStatusRule(int status, bool expected)
    {
        Assert.Equal(expected, OrderRequestStatus.IsResendAllowed(status));
    }
}
