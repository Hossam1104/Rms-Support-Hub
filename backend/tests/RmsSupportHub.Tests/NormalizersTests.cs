using RmsSupportHub.Core;
using Xunit;

namespace RmsSupportHub.Tests;

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

    [Theory]
    [InlineData("+966556028080", "556028080")]
    [InlineData("966556028080", "556028080")]
    [InlineData("00966556028080", "556028080")]
    [InlineData("0556028080", "556028080")]
    [InlineData("556028080", "556028080")]           // already local -- unchanged
    [InlineData("+966 55 602 8080", "556028080")]    // separators dropped
    [InlineData("(966) 55-602-8080", "556028080")]
    public void NormalizeLocalPhone_StripsOnlyTheSaudiCountryCode(string input, string expected)
    {
        Assert.Equal(expected, Normalizers.NormalizeLocalPhone(input));
    }

    /// <summary>The reference payloads carry the country code in its own
    /// client_country_code key, so only a LEADING 966 belongs to the country
    /// code. A 966 that merely appears inside a subscriber number, and any
    /// non-Saudi number, must survive untouched.</summary>
    [Theory]
    [InlineData("509661234", "509661234")]           // 9 digits, 966 in the middle
    [InlineData("966966028080", "966028080")]        // leading 966 goes, inner 966 stays
    [InlineData("14155552671", "14155552671")]       // US number -- no Saudi rule matches
    [InlineData("00447911123456", "00447911123456")] // UK 00-prefixed -- not 00966
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeLocalPhone_PreservesEverythingElse(string? input, string expected)
    {
        Assert.Equal(expected, Normalizers.NormalizeLocalPhone(input));
    }
}
