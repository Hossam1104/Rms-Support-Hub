using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PosAdminTool.WinUI.ViewModels;

namespace PosAdminTool.WinUI.Views;

public sealed partial class ServicesPage : Page
{
    public ServicesPage()
    {
        InitializeComponent();
        ViewModel = App.Resolve<ServicesViewModel>();
        DataContext = ViewModel;
    }

    public ServicesViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.StopTimer();
    }
}
