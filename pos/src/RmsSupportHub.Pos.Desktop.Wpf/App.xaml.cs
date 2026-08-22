using System.Windows;
using RmsSupportHub.Pos.Desktop.Wpf.Services;
using RmsSupportHub.Pos.Desktop.Wpf.ViewModels;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Desktop.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var localIpcClient = new LocalIpcClient();
        var healthClient = new LocalAgentHealthClient(localIpcClient);
        var dashboard = new DashboardViewModel(healthClient);
        MainWindow = new MainWindow(dashboard);
        MainWindow.Show();
    }
}
