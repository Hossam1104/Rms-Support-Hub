using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RmsSupportHub.Pos.Application.UseCases;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using PosAdminTool.WinUI.Services;

namespace PosAdminTool.WinUI.ViewModels;

public sealed partial class OperationsViewModel(
    IConfigurationService configurationService,
    RunOperationUseCase runOperationUseCase,
    LogHub logHub)
    : BaseViewModel(logHub)
{
    private AppSettings _settings = new();

    [ObservableProperty]
    private bool backupBranchDatabase = true;

    [ObservableProperty]
    private bool backupCashierDatabase = true;

    [ObservableProperty]
    private bool backupBranchConfig = true;

    [ObservableProperty]
    private bool backupCashierServerConfig = true;

    [ObservableProperty]
    private bool backupCashierUiConfig = true;

    [ObservableProperty]
    private string restoreType = "Full";

    [ObservableProperty]
    private string targetDatabase = string.Empty;

    [ObservableProperty]
    private string backupZipPath = string.Empty;

    [ObservableProperty]
    private string dbFilesPath = string.Empty;

    [ObservableProperty]
    private double progressValue;

    [ObservableProperty]
    private bool riskAccepted;

    [ObservableProperty]
    private string progressText = string.Empty;

    [RelayCommand]
    public async Task LoadAsync()
    {
        _settings = await configurationService.LoadAsync();
        TargetDatabase = DatabaseResolver.ResolveBranchDatabase(_settings);
        DbFilesPath = _settings.DbFilesPath;
    }

    [RelayCommand]
    private void SelectAllBackupItems()
    {
        BackupBranchDatabase = true;
        BackupCashierDatabase = true;
        BackupBranchConfig = true;
        BackupCashierServerConfig = true;
        BackupCashierUiConfig = true;
    }

    [RelayCommand]
    private async Task RunBackupAsync()
    {
        ProgressValue = 0;
        try
        {
            await RunBusyAsync("Running backup...", async () =>
            {
                var progress = new Progress<string>(message =>
                {
                    ProgressText = message;
                    Log(message);
                });

                var result = await runOperationUseCase.BackupAsync(_settings, BuildSelectedItems(), progress);
                LogHub.AddResult("Backup", result);
                ProgressValue = result.Success ? 1 : 0;
                StatusMessage = result.Success ? "Backup complete" : string.Join(Environment.NewLine, result.Errors);
            });
        }
        finally
        {
            CleanupCommand.NotifyCanExecuteChanged();
            ResetBranchDataCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private async Task RestoreDatabaseAsync()
    {
        ProgressValue = 0;
        try
        {
            await RunBusyAsync("Restoring database...", async () =>
            {
                var progress = new Progress<string>(message =>
                {
                    ProgressText = message;
                    Log(message);
                });

                var result = await runOperationUseCase.RestoreAsync(_settings, BackupZipPath, TargetDatabase, DbFilesPath, RestoreType, progress);
                LogHub.AddResult("Restore", result);
                ProgressValue = result.Success ? 1 : 0;
                StatusMessage = result.Success ? "Restore complete" : string.Join(Environment.NewLine, result.Errors);
            });
        }
        finally
        {
            CleanupCommand.NotifyCanExecuteChanged();
            ResetBranchDataCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunDangerOperation))]
    private async Task CleanupAsync()
    {
        ProgressValue = 0;
        try
        {
            await RunBusyAsync("Running cleanup...", async () =>
            {
                var progress = new Progress<string>(message =>
                {
                    ProgressText = message;
                    Log(message);
                });

                var result = await runOperationUseCase.CleanupAsync(_settings, progress);
                LogHub.AddResult("Cleanup", result);
                StatusMessage = result.Success ? "Cleanup complete" : string.Join(Environment.NewLine, result.Errors);
            });
        }
        finally
        {
            CleanupCommand.NotifyCanExecuteChanged();
            ResetBranchDataCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunDangerOperation))]
    private async Task ResetBranchDataAsync()
    {
        ProgressValue = 0;
        try
        {
            await RunBusyAsync("Resetting branch data...", async () =>
            {
                var result = await runOperationUseCase.ResetBranchDataAsync(_settings);
                LogHub.AddResult("Reset branch data", result);
                StatusMessage = result.Success ? "Branch data reset" : string.Join(Environment.NewLine, result.Errors);
            });
        }
        finally
        {
            CleanupCommand.NotifyCanExecuteChanged();
            ResetBranchDataCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnRiskAcceptedChanged(bool value)
    {
        CleanupCommand.NotifyCanExecuteChanged();
        ResetBranchDataCommand.NotifyCanExecuteChanged();
    }

    private bool CanRunDangerOperation()
    {
        return RiskAccepted && !IsBusy;
    }

    private List<string> BuildSelectedItems()
    {
        var selected = new List<string>();
        if (BackupBranchDatabase)
        {
            selected.Add("RmsBranchSrv Database");
        }

        if (BackupCashierDatabase)
        {
            selected.Add("RmsCashierSrv Database");
        }

        if (BackupBranchConfig)
        {
            selected.Add("RMS_BranchService_appsettings.json");
        }

        if (BackupCashierServerConfig)
        {
            selected.Add("RMS_CashierServer_appsettings.json");
        }

        if (BackupCashierUiConfig)
        {
            selected.Add("RMS_CashierUI_appsettings.json");
        }

        return selected;
    }
}
