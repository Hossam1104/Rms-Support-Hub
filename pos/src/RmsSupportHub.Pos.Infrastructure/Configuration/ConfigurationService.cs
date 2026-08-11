using System.Text.Json;
using System.Text.Json.Nodes;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Configuration;

public sealed class ConfigurationService(ICryptoService cryptoService) : IConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = null,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private AppSettings _settings = new();

    public string? LastLoadError { get; private set; }

    public string ConfigFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".pos_admin_tool",
        "config.json");

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LastLoadError = null;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath)!);
            if (!File.Exists(ConfigFilePath))
            {
                await SaveCoreAsync(_settings, cancellationToken).ConfigureAwait(false);
                return _settings.Clone();
            }

            var json = await File.ReadAllTextAsync(ConfigFilePath, cancellationToken).ConfigureAwait(false);
            var cryptoSalt = ReadCryptoSalt(json);
            cryptoService.Initialize(cryptoSalt);

            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            _settings = ConfigMigrator.Repair(loaded);
            DecryptSecrets(_settings);

            if (string.IsNullOrWhiteSpace(_settings.SqlPassword))
            {
                _settings.SqlPassword = string.Empty;
            }

            return _settings.Clone();
        }
        catch (Exception ex)
        {
            LastLoadError = ex.Message;
            _settings = new AppSettings();
            return _settings.Clone();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _settings = ConfigMigrator.Repair(settings.Clone());
            await SaveCoreAsync(_settings, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AppSettings> UpdateAsync(Action<AppSettings> modifier, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var updated = _settings.Clone();
            modifier(updated);
            _settings = ConfigMigrator.Repair(updated);
            await SaveCoreAsync(_settings, cancellationToken).ConfigureAwait(false);
            return _settings.Clone();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveCoreAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath)!);
        var export = settings.Clone();
        EncryptSecrets(export);

        var node = JsonSerializer.SerializeToNode(export, JsonOptions) as JsonObject ?? [];
        node["crypto_salt"] = cryptoService.SaltBase64;

        var json = node.ToJsonString(JsonOptions);
        await File.WriteAllTextAsync(ConfigFilePath, json, cancellationToken).ConfigureAwait(false);
    }

    private void EncryptSecrets(AppSettings settings)
    {
        settings.SqlPassword = cryptoService.Encrypt(settings.SqlPassword);
        settings.DbDownloader.RdbPassword = cryptoService.Encrypt(settings.DbDownloader.RdbPassword);
    }

    private void DecryptSecrets(AppSettings settings)
    {
        settings.SqlPassword = cryptoService.Decrypt(settings.SqlPassword);
        settings.DbDownloader.RdbPassword = cryptoService.Decrypt(settings.DbDownloader.RdbPassword);
    }

    private static string? ReadCryptoSalt(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("crypto_salt", out var salt)
                ? salt.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
