using CommunityToolkit.Mvvm.ComponentModel;
using PosAdminTool.WinUI.Services;

namespace PosAdminTool.WinUI.ViewModels;

public abstract partial class BaseViewModel(LogHub logHub) : ObservableObject
{
    protected LogHub LogHub { get; } = logHub;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    protected void Log(string message)
    {
        LogHub.Add(message);
    }

    protected async Task RunBusyAsync(string message, Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = message;
            await action();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            Log(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
