using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PosAdminTool.WinUI.ViewModels;

namespace PosAdminTool.WinUI.Views;

public sealed partial class OperationsPage : Page
{
    public OperationsPage()
    {
        InitializeComponent();
        ViewModel = App.Resolve<OperationsViewModel>();
        DataContext = ViewModel;
    }

    public OperationsViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }
}
