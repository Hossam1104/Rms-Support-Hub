using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PosAdminTool.WinUI.ViewModels;

namespace PosAdminTool.WinUI.Views;

public sealed partial class ConfigurationPage : Page
{
    public ConfigurationPage()
    {
        InitializeComponent();
        ViewModel = App.Resolve<ConfigurationViewModel>();
        DataContext = ViewModel;
    }

    public ConfigurationViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadCommand.ExecuteAsync(null);
        SqlPasswordBox.Password = ViewModel.SqlPassword;
    }

    private void OnSqlPasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.SqlPassword = SqlPasswordBox.Password;
    }
}
