using Microsoft.UI.Xaml.Controls;
using PosAdminTool.WinUI.ViewModels;

namespace PosAdminTool.WinUI.Views;

public sealed partial class LogPage : Page
{
    public LogPage()
    {
        InitializeComponent();
        DataContext = App.Resolve<LogViewModel>();
    }
}
