using RmsSupportHub.Pos.Infrastructure.Configuration;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

public sealed class CryptoServiceTests
{
    [Fact]
    public void EncryptDecryptRoundTripsSecret()
    {
        var crypto = new CryptoService();

        var encrypted = crypto.Encrypt("secret-value");
        var decrypted = crypto.Decrypt(encrypted);

        Assert.NotEqual("secret-value", encrypted);
        Assert.Equal("secret-value", decrypted);
        Assert.StartsWith("aes1:", encrypted, StringComparison.Ordinal);
    }
}
