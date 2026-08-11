using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RmsSupportHub.Pos.Domain.Interfaces;
using PosAdminTool.WinUI.Services;
using PosAdminTool.WinUI.Views;

namespace PosAdminTool.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly IConfigurationService _configurationService;
    private readonly ThemeService _themeService;

    public MainWindow()
    {
        InitializeComponent();

        _configurationService = App.Resolve<IConfigurationService>();
        _themeService = App.Resolve<ThemeService>();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop();
        }

        var rootElement = (FrameworkElement)Content;
        _themeService.Initialize(rootElement);
        _themeService.ThemeChanged += (_, theme) => UpdateThemeStatus(theme);

        Activated += OnActivated;
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        var settings = await _configurationService.LoadAsync();
        _themeService.Apply(settings.Theme);
        UpdateThemeStatus(_themeService.CurrentTheme);

        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(ConfigurationPage));
        UpdateSectionStatus("Configuration");
    }

    private async void OnToggleThemeClicked(object sender, RoutedEventArgs e)
    {
        var settings = await _configurationService.LoadAsync();
        settings.Theme = string.Equals(settings.Theme, "DARK", StringComparison.OrdinalIgnoreCase) ? "LIGHT" : "DARK";
        await _configurationService.SaveAsync(settings);
        _themeService.Apply(settings.Theme);
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        var (pageType, title) = tag switch
        {
            "configuration" => (typeof(ConfigurationPage), "Configuration"),
            "services" => (typeof(ServicesPage), "Services"),
            "operations" => (typeof(OperationsPage), "Operations"),
            "dbdownloader" => (typeof(DbDownloaderPage), "DB Downloader"),
            "log" => (typeof(LogPage), "Log"),
            _ => (typeof(ConfigurationPage), "Configuration")
        };

        ContentFrame.Navigate(pageType);
        UpdateSectionStatus(title);
    }

    private void UpdateSectionStatus(string sectionTitle)
    {
        var themeLabel = _themeService.CurrentTheme == ElementTheme.Dark ? "Dark" : "Light";
        SectionStatusText.Text = $"{sectionTitle} · {themeLabel}";
    }

    private void UpdateThemeStatus(ElementTheme theme)
    {
        ThemeToggleButton.Content = theme == ElementTheme.Dark ? "☀️  Light" : "\U0001F319  Dark";

        var currentTag = (NavView.SelectedItem as NavigationViewItem)?.Content as string ?? "Configuration";
        UpdateSectionStatus(currentTag);
    }
}
