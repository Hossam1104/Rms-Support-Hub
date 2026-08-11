using System.Net;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Infrastructure.Configuration;

public sealed class CryptoService : ICryptoService
{
    private const int SaltLength = 16;
    private const int KeyLength = 64;
    private const int Iterations = 100_000;
    private const string Prefix = "aes1";

    private byte[] _salt = [];
    private byte[] _encryptionKey = [];
    private byte[] _hmacKey = [];

    public CryptoService()
    {
        Initialize();
    }

    public string SaltBase64 => Convert.ToBase64String(_salt);

    public void Initialize(string? storedSaltBase64 = null)
    {
        _salt = ResolveSalt(storedSaltBase64);
        var keyMaterial = DeriveKeyMaterial(_salt);
        _encryptionKey = keyMaterial[..32];
        _hmacKey = keyMaterial[32..];
    }

    public string Encrypt(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(value);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var tag = ComputeTag(aes.IV, cipherBytes);

        return string.Join(
            ':',
            Prefix,
            Convert.ToBase64String(aes.IV),
            Convert.ToBase64String(cipherBytes),
            Convert.ToBase64String(tag));
    }

    public string Decrypt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        try
        {
            var parts = value.Split(':', 4);
            if (parts.Length != 4 || parts[0] != Prefix)
            {
                return string.Empty;
            }

            var iv = Convert.FromBase64String(parts[1]);
            var cipherBytes = Convert.FromBase64String(parts[2]);
            var expectedTag = Convert.FromBase64String(parts[3]);
            var actualTag = ComputeTag(iv, cipherBytes);
            if (!CryptographicOperations.FixedTimeEquals(expectedTag, actualTag))
            {
                return string.Empty;
            }

            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    private byte[] ComputeTag(byte[] iv, byte[] cipherBytes)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        var payload = new byte[iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, payload, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, iv.Length, cipherBytes.Length);
        return hmac.ComputeHash(payload);
    }

    private static byte[] ResolveSalt(string? storedSaltBase64)
    {
        if (!string.IsNullOrWhiteSpace(storedSaltBase64))
        {
            try
            {
                var decoded = Convert.FromBase64String(storedSaltBase64);
                if (decoded.Length >= SaltLength)
                {
                    return decoded[..SaltLength];
                }
            }
            catch
            {
                // Fall back to a user-bound salt below.
            }
        }

        var identity = ResolveIdentity();
        return SHA256.HashData(Encoding.UTF8.GetBytes(identity))[..SaltLength];
    }

    private static string ResolveIdentity()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var sid = WindowsIdentity.GetCurrent().User?.Value;
                if (!string.IsNullOrWhiteSpace(sid))
                {
                    return sid;
                }
            }
            catch
            {
                // Fall back to host/user entropy.
            }
        }

        return $"{Dns.GetHostName()}:{Environment.UserName}";
    }

    private static byte[] DeriveKeyMaterial(byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes($"{Dns.GetHostName()}:{Environment.UserName}"),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeyLength);
    }
}
