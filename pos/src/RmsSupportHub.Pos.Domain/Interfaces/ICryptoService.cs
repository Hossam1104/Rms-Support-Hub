namespace RmsSupportHub.Pos.Domain.Interfaces;

public interface ICryptoService
{
    string SaltBase64 { get; }

    void Initialize(string? storedSaltBase64 = null);

    string Encrypt(string value);

    string Decrypt(string value);
}
