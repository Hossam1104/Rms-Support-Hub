using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PosAdminTool.WinUI.ViewModels;

namespace PosAdminTool.WinUI.Views;

public sealed partial class DbDownloaderPage : Page
{
    private bool _isSyncingSelection;

    public DbDownloaderPage()
    {
        InitializeComponent();
        ViewModel = App.Resolve<DbDownloaderViewModel>();
        DataContext = ViewModel;
    }

    public DbDownloaderViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadCommand.ExecuteAsync(null);
        RdbPasswordBox.Password = ViewModel.RdbPassword;
    }

    private void OnRdbPasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.RdbPassword = RdbPasswordBox.Password;
    }

    private void OnBranchSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        _isSyncingSelection = true;
        try
        {
            ViewModel.SelectedBranchCodes.Clear();
            foreach (var item in BranchListView.SelectedItems)
            {
                if (item is string code)
                {
                    ViewModel.SelectedBranchCodes.Add(code);
                }
            }
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void OnRemoveBranchClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string code })
        {
            ViewModel.RemoveBranchCodeCommand.Execute(code);
        }
    }
}
