using OnlineOrderTool.Core;
using Xunit;

namespace OnlineOrderTool.Tests;

public class NormalizersTests
{
    [Theory]
    [InlineData("212401", "000000000000212401")]
    [InlineData("000000000000212401", "000000000000212401")]
    [InlineData("000001", "000000000000000001")]
    public void NormalizeUpcMaterialNumber_PadsOrPassesThrough(string input, string expected)
    {
        Assert.Equal(expected, Normalizers.NormalizeUpcMaterialNumber(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]      // 5 digits -- neither 6 nor 18
    [InlineData("1234567")]    // 7 digits
    [InlineData("ABCDEF")]     // not numeric
    [InlineData("21240X")]     // mixed
    public void NormalizeUpcMaterialNumber_RejectsInvalidInput(string input)
    {
        Assert.ThrowsAny<ArgumentException>(() => Normalizers.NormalizeUpcMaterialNumber(input));
    }

    [Theory]
    [InlineData("+966556028080", "556028080")]
    [InlineData("966556028080", "556028080")]
    [InlineData("0556028080", "556028080")]
    [InlineData("556028080", "556028080")]
    [InlineData("+966 55 602 8080", "556028080")] // spaces stripped along with the +
    public void NormalizePhoneSearch_CollapsesAllFormsToTheSame9Digits(string input, string expected)
    {
        Assert.Equal(expected, Normalizers.NormalizePhoneSearch(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345678")]  // 8 digits -- one short
    [InlineData("abc")]
    public void NormalizePhoneSearch_RejectsTooShortInput(string input)
    {
        Assert.ThrowsAny<ArgumentException>(() => Normalizers.NormalizePhoneSearch(input));
    }
}
