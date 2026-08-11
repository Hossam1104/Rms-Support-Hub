using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Configuration;

public static class ConfigMigrator
{
    public static AppSettings Repair(AppSettings? loaded)
    {
        var defaults = new AppSettings();
        var settings = loaded ?? defaults;

        settings.Version = string.IsNullOrWhiteSpace(settings.Version) ? defaults.Version : settings.Version;
        settings.Theme = string.IsNullOrWhiteSpace(settings.Theme) ? defaults.Theme : settings.Theme;
        settings.SqlInstance = string.IsNullOrWhiteSpace(settings.SqlInstance) ? defaults.SqlInstance : settings.SqlInstance;
        settings.SqlUser = string.IsNullOrWhiteSpace(settings.SqlUser) ? defaults.SqlUser : settings.SqlUser;
        settings.ClientName = string.IsNullOrWhiteSpace(settings.ClientName) ? defaults.ClientName : settings.ClientName;
        settings.BackupFolder = string.IsNullOrWhiteSpace(settings.BackupFolder) ? defaults.BackupFolder : settings.BackupFolder;
        settings.DbFilesPath = string.IsNullOrWhiteSpace(settings.DbFilesPath) ? defaults.DbFilesPath : settings.DbFilesPath;
        settings.Release = string.IsNullOrWhiteSpace(settings.Release) ? defaults.Release : settings.Release;
        settings.Databases = settings.Databases is { Count: > 0 } ? settings.Databases : defaults.Databases;
        settings.Services = settings.Services is { Count: > 0 } ? settings.Services : defaults.Services;
        settings.FoldersToDelete = settings.FoldersToDelete is { Count: > 0 } ? settings.FoldersToDelete : defaults.FoldersToDelete;
        settings.BranchConfigPath = string.IsNullOrWhiteSpace(settings.BranchConfigPath) ? defaults.BranchConfigPath : settings.BranchConfigPath;
        settings.CashierGrpcConfigPath = string.IsNullOrWhiteSpace(settings.CashierGrpcConfigPath) ? defaults.CashierGrpcConfigPath : settings.CashierGrpcConfigPath;
        settings.CashierUiConfigPath = string.IsNullOrWhiteSpace(settings.CashierUiConfigPath) ? defaults.CashierUiConfigPath : settings.CashierUiConfigPath;
        settings.RmsInfoPath = string.IsNullOrWhiteSpace(settings.RmsInfoPath) ? defaults.RmsInfoPath : settings.RmsInfoPath;
        settings.ReleasePath = string.IsNullOrWhiteSpace(settings.ReleasePath) ? defaults.ReleasePath : settings.ReleasePath;

        settings.DbDownloader ??= new();
        settings.DbDownloader.ApiUrl = string.IsNullOrWhiteSpace(settings.DbDownloader.ApiUrl) ? defaults.DbDownloader.ApiUrl : settings.DbDownloader.ApiUrl;
        settings.DbDownloader.BackupRootFolder = string.IsNullOrWhiteSpace(settings.DbDownloader.BackupRootFolder) ? defaults.DbDownloader.BackupRootFolder : settings.DbDownloader.BackupRootFolder;
        settings.DbDownloader.KnownBranchCodes ??= [];
        settings.DbDownloader.PollIntervalSeconds = settings.DbDownloader.PollIntervalSeconds > 0 ? settings.DbDownloader.PollIntervalSeconds : defaults.DbDownloader.PollIntervalSeconds;
        settings.DbDownloader.TimeoutSeconds = settings.DbDownloader.TimeoutSeconds > 0 ? settings.DbDownloader.TimeoutSeconds : defaults.DbDownloader.TimeoutSeconds;

        return settings;
    }
}
