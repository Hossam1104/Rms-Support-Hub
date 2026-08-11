using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RmsSupportHub.Pos.Domain.Enums;

namespace PosAdminTool.WinUI.ViewModels;

public sealed partial class BranchBackupRowViewModel(string branchCode, Func<BranchBackupRowViewModel, Task> onDownload) : ObservableObject
{
    public string BranchCode { get; } = branchCode;

    [ObservableProperty]
    private BranchBackupStatus status = BranchBackupStatus.Pending;

    [ObservableProperty]
    private double downloadProgress;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? remoteZipPath;

    [ObservableProperty]
    private string? localDownloadPath;

    public bool CanDownload => Status == BranchBackupStatus.Ready;

    partial void OnStatusChanged(BranchBackupStatus value)
    {
        OnPropertyChanged(nameof(CanDownload));
        DownloadCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private Task DownloadAsync()
    {
        return onDownload(this);
    }
}
