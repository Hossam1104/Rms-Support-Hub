using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using PosAdminTool.WinUI.Services;

namespace PosAdminTool.WinUI.ViewModels;

public sealed partial class DbDownloaderViewModel(
    IConfigurationService configurationService,
    DbDownloadService downloadService,
    LogHub logHub)
    : BaseViewModel(logHub)
{
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private AppSettings _settings = new();
    private CancellationTokenSource? _jobCts;

    public ObservableCollection<string> KnownBranchCodes { get; } = [];

    public ObservableCollection<string> FilteredBranchCodes { get; } = [];

    public ObservableCollection<string> SelectedBranchCodes { get; } = [];

    public ObservableCollection<BranchBackupRowViewModel> Jobs { get; } = [];

    [ObservableProperty]
    private string newBranchCode = string.Empty;

    [ObservableProperty]
    private string branchFilterText = string.Empty;

    [ObservableProperty]
    private string apiUrl = string.Empty;

    [ObservableProperty]
    private string rdbServerIp = string.Empty;

    [ObservableProperty]
    private string rdbUsername = string.Empty;

    [ObservableProperty]
    private string rdbPassword = string.Empty;

    [ObservableProperty]
    private string backupRootFolder = string.Empty;

    [ObservableProperty]
    private bool isJobRunning;

    [RelayCommand]
    public async Task LoadAsync()
    {
        _settings = await configurationService.LoadAsync();
        var downloader = _settings.DbDownloader;

        ApiUrl = downloader.ApiUrl;
        RdbServerIp = downloader.RdbServerIp;
        RdbUsername = downloader.RdbUsername;
        RdbPassword = downloader.RdbPassword;
        BackupRootFolder = downloader.BackupRootFolder;

        KnownBranchCodes.Clear();
        foreach (var code in downloader.KnownBranchCodes)
        {
            KnownBranchCodes.Add(code);
        }

        RefreshFilteredBranchCodes();
    }

    [RelayCommand]
    private void AddBranchCode()
    {
        var code = NewBranchCode.Trim();
        if (string.IsNullOrWhiteSpace(code) || KnownBranchCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        KnownBranchCodes.Add(code);
        NewBranchCode = string.Empty;
        RefreshFilteredBranchCodes();
    }

    [RelayCommand]
    private void RemoveBranchCode(string code)
    {
        KnownBranchCodes.Remove(code);
        SelectedBranchCodes.Remove(code);
        RefreshFilteredBranchCodes();
    }

    partial void OnBranchFilterTextChanged(string value)
    {
        RefreshFilteredBranchCodes();
    }

    private void RefreshFilteredBranchCodes()
    {
        var filter = BranchFilterText.Trim();
        var matches = string.IsNullOrEmpty(filter)
            ? KnownBranchCodes
            : KnownBranchCodes.Where(code => code.Contains(filter, StringComparison.OrdinalIgnoreCase));

        FilteredBranchCodes.Clear();
        foreach (var code in matches)
        {
            FilteredBranchCodes.Add(code);
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        await RunBusyAsync("Saving DB Downloader settings...", async () =>
        {
            _settings = await configurationService.LoadAsync();
            ApplyToSettings(_settings);
            await configurationService.SaveAsync(_settings);
            StatusMessage = "DB Downloader settings saved";
        });
    }

    [RelayCommand(CanExecute = nameof(CanTriggerJob))]
    private async Task TriggerJobAsync()
    {
        if (SelectedBranchCodes.Count == 0)
        {
            StatusMessage = "Select at least one branch";
            return;
        }

        _settings = await configurationService.LoadAsync();
        ApplyToSettings(_settings);
        await configurationService.SaveAsync(_settings);

        Jobs.Clear();
        var rows = new Dictionary<string, BranchBackupRowViewModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in SelectedBranchCodes)
        {
            var row = new BranchBackupRowViewModel(code, DownloadBranchAsync);
            rows[code] = row;
            Jobs.Add(row);
        }

        _jobCts = new CancellationTokenSource();
        IsJobRunning = true;
        TriggerJobCommand.NotifyCanExecuteChanged();

        try
        {
            var progress = new Progress<string>(Log);
            var branchCodes = SelectedBranchCodes.ToList();
            var job = await downloadService.RunAsync(
                _settings.DbDownloader,
                branchCodes,
                onItemChanged: item =>
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (rows.TryGetValue(item.BranchCode, out var row))
                        {
                            row.Status = item.Status;
                            row.RemoteZipPath = item.RemoteZipPath;
                            row.ErrorMessage = item.ErrorMessage;
                        }
                    });
                },
                progress: progress,
                cancellationToken: _jobCts.Token);

            StatusMessage = job.IsComplete
                ? $"Batch complete — folder {job.BatchFolderPath}"
                : "Batch job ended before all branches finished";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Job failed: {ex.Message}";
            Log(ex.Message);
        }
        finally
        {
            IsJobRunning = false;
            TriggerJobCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task DownloadBranchAsync(BranchBackupRowViewModel row)
    {
        var localFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "PosAdminTool_DbBackups");

        try
        {
            var progress = new Progress<double>(value =>
                _dispatcherQueue.TryEnqueue(() => row.DownloadProgress = value));

            await downloadService.DownloadAsync(
                _settings.DbDownloader,
                new BranchBackupItem(row.BranchCode) { RemoteZipPath = row.RemoteZipPath },
                localFolder,
                progress,
                CancellationToken.None);

            row.Status = RmsSupportHub.Pos.Domain.Enums.BranchBackupStatus.Downloaded;
            row.LocalDownloadPath = Path.Combine(localFolder, Path.GetFileName(row.RemoteZipPath!));
            Log($"{row.BranchCode}: downloaded to {row.LocalDownloadPath}");
        }
        catch (Exception ex)
        {
            row.Status = RmsSupportHub.Pos.Domain.Enums.BranchBackupStatus.Failed;
            row.ErrorMessage = ex.Message;
            Log($"{row.BranchCode}: download failed - {ex.Message}");
        }
    }

    private bool CanTriggerJob() => !IsJobRunning;

    private void ApplyToSettings(AppSettings settings)
    {
        settings.DbDownloader.ApiUrl = ApiUrl.Trim();
        settings.DbDownloader.RdbServerIp = RdbServerIp.Trim();
        settings.DbDownloader.RdbUsername = RdbUsername.Trim();
        settings.DbDownloader.RdbPassword = RdbPassword;
        settings.DbDownloader.BackupRootFolder = BackupRootFolder.Trim();
        settings.DbDownloader.KnownBranchCodes = [.. KnownBranchCodes];
    }
}
