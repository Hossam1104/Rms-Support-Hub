using System.Text.Json;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.UseCases;

public sealed class ImportFromRmsUseCase(IConfigurationService configurationService)
{
    private static readonly string[] ReleasePropertyNames =
    [
        "ReleaseNumber",
        "Release",
        "RMSRelease",
        "RmsRelease",
        "Version",
        "RMSVersion",
        "RmsVersion"
    ];

    private static readonly string[] ReleaseFileNames =
    [
        "ReleaseNumber.txt",
        "Release.txt",
        "Version.txt",
        "version.txt"
    ];

    public async Task<OperationResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var result = OperationResult.Running("import_from_rms");
        var settings = await configurationService.LoadAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(settings.RmsInfoPath) || !File.Exists(settings.RmsInfoPath))
        {
            result.AddError($"RMSInfo.json not found at: {settings.RmsInfoPath}");
            result.Finalize(OperationStatus.Failed);
            return result;
        }

        try
        {
            using var rmsInfoStream = File.OpenRead(settings.RmsInfoPath);
            using var rmsInfo = await JsonDocument.ParseAsync(rmsInfoStream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = rmsInfo.RootElement;

            settings.SqlInstance = ReadString(root, "ServerName", ".") ?? ".";
            settings.SqlUser = ReadString(root, "UserName", "sa") ?? "sa";
            if (string.IsNullOrWhiteSpace(settings.SqlUser))
            {
                settings.SqlUser = "sa";
            }
            var password = ReadString(root, "Password", null);
            if (!string.IsNullOrWhiteSpace(password))
            {
                settings.SqlPassword = password;
            }
            settings.BranchCode = ReadString(root, "BranchCode", string.Empty) ?? string.Empty;
            settings.PosNumber = ReadString(root, "POSNumber", string.Empty) ?? string.Empty;

            var mainServerIp = ReadString(root, "MainServerIP", string.Empty);
            if (!string.IsNullOrWhiteSpace(mainServerIp))
            {
                var protocol = ReadString(root, "Protocol", "http") ?? "http";
                settings.ApiBaseUrl = $"{protocol}://{mainServerIp}";
            }

            if (!string.IsNullOrWhiteSpace(settings.BranchCode))
            {
                var defaultBackup = $@"D:\DB Backups\{settings.BranchCode}";
                if (string.IsNullOrWhiteSpace(settings.BackupFolder) || settings.BackupFolder == @"D:\DB Backups")
                {
                    settings.BackupFolder = defaultBackup;
                }

                if (string.IsNullOrWhiteSpace(settings.DbFilesPath) || settings.DbFilesPath == @"D:\DB Backups")
                {
                    settings.DbFilesPath = defaultBackup;
                }
            }

            await ReadReleaseAsync(root, settings, result, cancellationToken).ConfigureAwait(false);

            await ReadClientNameAsync(settings, result, cancellationToken).ConfigureAwait(false);
            await configurationService.SaveAsync(settings, cancellationToken).ConfigureAwait(false);

            result.AddMessage($"Imported from RMS+ (Branch: {settings.BranchCode}, POS: {settings.PosNumber})");
            result.Context["branch_code"] = settings.BranchCode;
            result.Context["pos_number"] = settings.PosNumber;
            result.Finalize(OperationStatus.Success);
            return result;
        }
        catch (Exception ex)
        {
            result.AddError($"Failed to import RMS+ settings: {ex.Message}");
            result.Finalize(OperationStatus.Failed);
            return result;
        }
    }

    private static async Task ReadReleaseAsync(JsonElement rmsInfoRoot, AppSettings settings, OperationResult result, CancellationToken cancellationToken)
    {
        var release = ReadFirstString(rmsInfoRoot, ReleasePropertyNames);
        if (string.IsNullOrWhiteSpace(release))
        {
            release = await ReadReleaseFromFilesAsync(settings, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(release))
        {
            result.AddMessage("Release number was not found.");
            return;
        }

        settings.Release = release.Trim();
        result.AddMessage($"Release number loaded: {settings.Release}");
    }

    private static async Task<string?> ReadReleaseFromFilesAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        foreach (var path in EnumerateReleaseCandidatePaths(settings))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var release = (await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).Trim();
            if (!string.IsNullOrWhiteSpace(release))
            {
                return release;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateReleaseCandidatePaths(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ReleasePath))
        {
            yield return settings.ReleasePath;
        }

        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RMS_Plus"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RMS_Plus_ReleaseRepo")
        };

        foreach (var root in roots)
        {
            foreach (var fileName in ReleaseFileNames)
            {
                yield return Path.Combine(root, fileName);
            }
        }
    }

    private static async Task ReadClientNameAsync(AppSettings settings, OperationResult result, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.CashierUiConfigPath) || !File.Exists(settings.CashierUiConfigPath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(settings.CashierUiConfigPath);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("Settings", out var settingsElement)
                && settingsElement.ValueKind == JsonValueKind.Object
                && settingsElement.TryGetProperty("TheClient", out var clientElement))
            {
                var client = clientElement.ValueKind == JsonValueKind.String
                    ? clientElement.GetString()
                    : clientElement.ToString();
                if (!string.IsNullOrWhiteSpace(client))
                {
                    settings.ClientName = client;
                    result.AddMessage($"Client name loaded: {client}");
                }
            }
        }
        catch (Exception ex)
        {
            result.AddMessage($"Could not read client name from CashierUI config: {ex.Message}");
        }
    }

    private static string? ReadFirstString(JsonElement element, IEnumerable<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = ReadString(element, propertyName, null);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName, string? fallback)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return fallback;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => fallback
        };
    }
}
