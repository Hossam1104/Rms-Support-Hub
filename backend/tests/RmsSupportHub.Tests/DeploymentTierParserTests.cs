using RmsSupportHub.Core.Modules;
using Xunit;

namespace RmsSupportHub.Tests;

public sealed class DeploymentTierParserTests
{
    [Theory]
    [InlineData("Testing", DeploymentTier.Testing)]
    [InlineData("testing", DeploymentTier.Testing)]
    [InlineData("TESTING", DeploymentTier.Testing)]
    [InlineData("Production", DeploymentTier.Production)]
    [InlineData("production", DeploymentTier.Production)]
    [InlineData("PRODUCTION", DeploymentTier.Production)]
    public void TryParseExact_AcceptsIntendedTextualTokens(string value, DeploymentTier expected)
    {
        var accepted = DeploymentTierParser.TryParseExact(value, out var tier);

        Assert.True(accepted);
        Assert.Equal(expected, tier);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("2")]
    [InlineData("01")]
    [InlineData("+1")]
    [InlineData("Staging")]
    [InlineData("Testing;Production")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" Testing")]
    [InlineData("Testing ")]
    [InlineData(" Production")]
    [InlineData("Production ")]
    [InlineData(null)]
    public void TryParseExact_RejectsNumericAndMalformedValues(string? value)
    {
        var accepted = DeploymentTierParser.TryParseExact(value, out var tier);

        Assert.False(accepted);
        Assert.Equal(default, tier);
    }
}
