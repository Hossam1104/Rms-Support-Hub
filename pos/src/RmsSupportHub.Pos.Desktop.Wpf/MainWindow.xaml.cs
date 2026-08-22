using System.Windows;
using RmsSupportHub.Pos.Desktop.Wpf.ViewModels;

namespace RmsSupportHub.Pos.Desktop.Wpf;

public partial class MainWindow : Window
{
    private readonly DashboardViewModel dashboard;
    private bool started;

    public MainWindow(DashboardViewModel dashboard)
    {
        this.dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        InitializeComponent();
        DataContext = dashboard;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (started)
        {
            return;
        }

        started = true;
        await dashboard.InitializeAsync();
    }

    private void OnClosed(object? sender, EventArgs e) => dashboard.Dispose();
}
