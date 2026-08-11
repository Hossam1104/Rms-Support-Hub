using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Application.UseCases;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Infrastructure.Configuration;
using RmsSupportHub.Pos.Infrastructure.Backups;
using RmsSupportHub.Pos.Infrastructure.Http;
using RmsSupportHub.Pos.Infrastructure.Smb;
using RmsSupportHub.Pos.Infrastructure.Windows;
using PosAdminTool.WinUI.Services;
using PosAdminTool.WinUI.ViewModels;

namespace PosAdminTool.WinUI;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;
    private readonly bool _exitAfterElevation;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => LogCrash("AppDomain", e.ExceptionObject as Exception);
        UnhandledException += (s, e) => { LogCrash("Xaml", e.Exception); e.Handled = true; };
        TaskScheduler.UnobservedTaskException += (s, e) => LogCrash("Task", e.Exception);

        InitializeComponent();
        Services = BuildServiceProvider();

        var adminPrivilegeManager = Services.GetRequiredService<AdminPrivilegeManager>();
        if (!adminPrivilegeManager.IsAdministrator() && adminPrivilegeManager.RequestAdministrator())
        {
            _exitAfterElevation = true;
        }
    }

    private void MergeResourceDictionaries()
    {
        // WindowsAppSDK 1.8's unpackaged XAML markup compiler cannot convert a
        // ResourceDictionary.Source string literal to Uri (XamlParseException,
        // HRESULT 0x802b000a). Constructing the Uri in code bypasses the broken
        // markup type converter. See microsoft/microsoft-ui-xaml#6674.
        //
        // The default Fluent control resources (e.g. TabViewButtonBackground, used by
        // NavigationView's default style) aren't merged automatically in this unpackaged
        // build, so they're added explicitly and first, ahead of the app's own overrides.
        Resources.MergedDictionaries.Add(new Microsoft.UI.Xaml.Controls.XamlControlsResources());
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Resources/Colors.xaml") });
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Resources/Styles.xaml") });
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "pos_admin_crash.txt");
            var msg = $"[{DateTime.Now}] Source: {source}\nException: {ex?.ToString() ?? "null"}\nInnerException: {ex?.InnerException?.ToString() ?? "null"}\n\n";
            File.AppendAllText(path, msg);
        }
        catch {}
    }

    public static IServiceProvider Services { get; private set; } = null!;

    public static T Resolve<T>()
        where T : notnull
    {
        return Services.GetRequiredService<T>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_exitAfterElevation)
        {
            Exit();
            return;
        }

        MergeResourceDictionaries();
        _window = new MainWindow();
        _window.Activate();
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ICryptoService, CryptoService>();
        services.AddSingleton<IServiceManager, WindowsServiceManager>();
        services.AddSingleton<IConnectivityMonitor, ConnectivityMonitor>();
        services.AddSingleton<IDatabaseService, SqlCmdExecutor>();
        services.AddSingleton<IBackupFileSystem, PhysicalBackupFileSystem>();
        services.AddSingleton<IMaintenanceFileSystem, PhysicalMaintenanceFileSystem>();
        services.AddSingleton<AdminPrivilegeManager>();
        services.AddSingleton<IHostAddressResolver, SystemHostAddressResolver>();
        services.AddSingleton(new HttpClient(
            BackupApiHttpMessageHandlerFactory.Create(new SystemHostAddressResolver())));
        services.AddSingleton<IBackupApiClient, BackupApiClient>();
        services.AddSingleton<IBackupRepository, SmbBackupRepository>();

        services.AddTransient<BackupService>();
        services.AddTransient<RestoreService>();
        services.AddTransient<BranchVerificationService>();
        services.AddTransient<CleanupService>();
        services.AddTransient<DbDownloadService>();

        services.AddTransient<TestConnectionUseCase>();
        services.AddTransient<ImportFromRmsUseCase>();
        services.AddTransient<RunOperationUseCase>();

        services.AddSingleton<LogHub>();
        services.AddSingleton<ThemeService>();

        services.AddTransient<ConfigurationViewModel>();
        services.AddTransient<ServicesViewModel>();
        services.AddTransient<OperationsViewModel>();
        services.AddTransient<DbDownloaderViewModel>();
        services.AddTransient<LogViewModel>();

        return services.BuildServiceProvider();
    }
}
