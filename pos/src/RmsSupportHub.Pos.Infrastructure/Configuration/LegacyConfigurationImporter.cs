using System.Text.Json;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Configuration;

/// <summary>
/// One-time, non-secret importer for the legacy <c>%USERPROFILE%\.pos_admin_tool\config.json</c>
/// (plan section 5.5). Never reads <c>sql_password</c> or <c>db_downloader.rdb_password</c> — the
/// legacy key derivation is bound to the interactive user identity and a Windows Service cannot
/// reproduce it (ADR-004). Only ever opens the legacy file for reading, so it is never modified.
/// Gated by a persisted marker so a second call is a strict no-op.
/// </summary>
public sealed class LegacyConfigurationImporter(
    LegacyConfigurationImporterOptions importerOptions,
    AgentConfigurationStoreOptions storeOptions,
    IAgentConfigurationStore configurationStore) : ILegacyConfigurationImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _sourceFilePath = importerOptions.SourceFilePath;
    private readonly string _rootDirectory = storeOptions.RootDirectory;

    private string MarkerFilePath => Path.Combine(_rootDirectory, "legacy-import-state.json");

    public async Task<LegacyImportResult> ImportAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await TryReadMarkerAsync(cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                return existing;
            }

            var result = await RunImportAsync(cancellationToken).ConfigureAwait(false);
            await WriteMarkerAsync(result, cancellationToken).ConfigureAwait(false);
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<LegacyImportResult> RunImportAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_sourceFilePath))
        {
            return new LegacyImportResult { SourceFound = false, Succeeded = true, ImportedAtUtc = DateTimeOffset.UtcNow };
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(_sourceFilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            return Failure($"Unable to read legacy config.json: {ex.GetType().Name}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Failure($"Unable to read legacy config.json: {ex.GetType().Name}");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return Failure("Legacy config.json is not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Failure("Legacy config.json root is not an object.");
            }

            var config = await configurationStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            var fieldsImported = new List<string>();

            ImportString(root, "sql_instance", v => config.SqlInstance = v, fieldsImported, "SqlInstance");
            ImportString(root, "sql_user", v => config.SqlUser = v, fieldsImported, "SqlUser");
            ImportString(root, "branch_code", v => config.BranchCode = v, fieldsImported, "BranchCode");
            ImportString(root, "pos_number", v => config.PosNumber = v, fieldsImported, "PosNumber");
            ImportString(root, "release", v => config.Release = v, fieldsImported, "Release");
            ImportString(root, "client_name", v => config.ClientName = v, fieldsImported, "ClientName");
            ImportString(root, "api_base_url", v => config.ApiBaseUrl = v, fieldsImported, "ApiBaseUrl");
            ImportString(root, "backup_folder", v => config.BackupFolder = v, fieldsImported, "BackupFolder");
            ImportStringArray(root, "databases", v => config.Databases = v, fieldsImported, "Databases");
            ImportStringArray(root, "services", v => config.Services = v, fieldsImported, "Services");

            if (root.TryGetProperty("db_downloader", out var downloader) && downloader.ValueKind == JsonValueKind.Object)
            {
                ImportString(downloader, "api_url", v => config.Downloader.ApiUrl = v, fieldsImported, "Downloader.ApiUrl");
                ImportString(downloader, "rdb_server_ip", v => config.Downloader.RdbServerIp = v, fieldsImported, "Downloader.RdbServerIp");
                ImportString(downloader, "rdb_username", v => config.Downloader.RdbUsername = v, fieldsImported, "Downloader.RdbUsername");
                ImportString(downloader, "backup_root_folder", v => config.Downloader.BackupRootFolder = v, fieldsImported, "Downloader.BackupRootFolder");
                ImportStringArray(downloader, "known_branch_codes", v => config.Downloader.KnownBranchCodes = v, fieldsImported, "Downloader.KnownBranchCodes");
                ImportInt(downloader, "poll_interval_seconds", v => config.Downloader.PollIntervalSeconds = v, fieldsImported, "Downloader.PollIntervalSeconds");
                ImportInt(downloader, "timeout_seconds", v => config.Downloader.TimeoutSeconds = v, fieldsImported, "Downloader.TimeoutSeconds");
            }

            if (fieldsImported.Count > 0)
            {
                config.Version += 1;
                await configurationStore.SaveAsync(config, cancellationToken).ConfigureAwait(false);
            }

            return new LegacyImportResult
            {
                SourceFound = true,
                Succeeded = true,
                FieldsImported = fieldsImported,
                ImportedAtUtc = DateTimeOffset.UtcNow
            };
        }
    }

    private static LegacyImportResult Failure(string reason) => new()
    {
        SourceFound = true,
        Succeeded = false,
        FailureReason = reason,
        ImportedAtUtc = DateTimeOffset.UtcNow
    };

    private static void ImportString(JsonElement element, string propertyName, Action<string> setter, List<string> tracker, string fieldLabel)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        setter(value);
        tracker.Add(fieldLabel);
    }

    private static void ImportStringArray(JsonElement element, string propertyName, Action<List<string>> setter, List<string> tracker, string fieldLabel)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var values = property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (values.Count == 0)
        {
            return;
        }

        setter(values);
        tracker.Add(fieldLabel);
    }

    private static void ImportInt(JsonElement element, string propertyName, Action<int> setter, List<string> tracker, string fieldLabel)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return;
        }

        if (!property.TryGetInt32(out var value))
        {
            return;
        }

        setter(value);
        tracker.Add(fieldLabel);
    }

    private async Task<LegacyImportResult?> TryReadMarkerAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(MarkerFilePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(MarkerFilePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<LegacyImportResult>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task WriteMarkerAsync(LegacyImportResult result, CancellationToken cancellationToken)
    {
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(_rootDirectory);
        var json = JsonSerializer.Serialize(result, JsonOptions);
        await AtomicFileWriter.WriteAsync(MarkerFilePath, json, cancellationToken).ConfigureAwait(false);
    }
}
