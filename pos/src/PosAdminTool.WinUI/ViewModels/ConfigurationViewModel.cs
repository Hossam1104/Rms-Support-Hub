using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Application.UseCases;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using PosAdminTool.WinUI.Services;

namespace PosAdminTool.WinUI.ViewModels;

public sealed partial class ConfigurationViewModel(
    IConfigurationService configurationService,
    IConnectivityMonitor connectivityMonitor,
    TestConnectionUseCase testConnectionUseCase,
    ImportFromRmsUseCase importFromRmsUseCase,
    BranchVerificationService branchVerificationService,
    ThemeService themeService,
    LogHub logHub)
    : BaseViewModel(logHub)
{
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private bool _autoImportAttempted;
    private AppSettings _settings = new();

    [ObservableProperty]
    private string sqlInstance = ".";

    [ObservableProperty]
    private string sqlUser = "sa";

    [ObservableProperty]
    private string sqlPassword = string.Empty;

    [ObservableProperty]
    private string clientName = string.Empty;

    [ObservableProperty]
    private string branchCode = string.Empty;

    [ObservableProperty]
    private string posNumber = string.Empty;

    [ObservableProperty]
    private string apiBaseUrl = string.Empty;

    [ObservableProperty]
    private string backupFolder = string.Empty;

    [ObservableProperty]
    private string dbFilesPath = string.Empty;

    [ObservableProperty]
    private string release = string.Empty;

    [ObservableProperty]
    private string databasesText = string.Empty;

    [ObservableProperty]
    private string servicesText = string.Empty;

    [ObservableProperty]
    private string serverStatusText = "Offline";

    [ObservableProperty]
    private bool isServerOnline;

    public string ConfigPath => configurationService.ConfigFilePath;

    [RelayCommand]
    public async Task LoadAsync()
    {
        _settings = await configurationService.LoadAsync();
        Apply(_settings);

        if (!string.IsNullOrWhiteSpace(configurationService.LastLoadError))
        {
            Log($"Config load warning: {configurationService.LastLoadError}");
            StatusMessage = $"Config could not be fully loaded: {configurationService.LastLoadError}";
        }

        if (!_autoImportAttempted && string.IsNullOrWhiteSpace(_settings.BranchCode))
        {
            _autoImportAttempted = true;
            try
            {
                var result = await importFromRmsUseCase.ExecuteAsync();
                LogHub.AddResult("Auto-import from RMS+", result);
                if (result.Success)
                {
                    _settings = await configurationService.LoadAsync();
                    Apply(_settings);
                    StatusMessage = "RMS+ settings auto-imported on first launch";
                }
            }
            catch (Exception ex)
            {
                Log($"Auto-import failed: {ex.Message}");
            }
        }

        connectivityMonitor.SetApiUrl(ApiBaseUrl);
        connectivityMonitor.StatusChanged -= OnConnectivityChanged;
        connectivityMonitor.StatusChanged += OnConnectivityChanged;

        if (connectivityMonitor.LastStatus is bool lastStatus)
        {
            OnConnectivityChanged(connectivityMonitor, lastStatus);
        }

        await connectivityMonitor.StartAsync();
        Log("Configuration loaded");
    }

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        await RunBusyAsync("Saving configuration...", async () =>
        {
            _settings = Read();
            await configurationService.SaveAsync(_settings);
            connectivityMonitor.SetApiUrl(_settings.ApiBaseUrl);
            Log("Settings saved");
            StatusMessage = "Settings saved";
        });
    }

    [RelayCommand]
    private async Task ImportFromRmsAsync()
    {
        await RunBusyAsync("Importing RMS+ settings...", async () =>
        {
            var result = await importFromRmsUseCase.ExecuteAsync();
            LogHub.AddResult("Import from RMS+", result);
            _settings = await configurationService.LoadAsync();
            Apply(_settings);
            StatusMessage = result.Success ? "RMS+ settings imported" : string.Join(Environment.NewLine, result.Errors);
        });
    }

    [RelayCommand]
    private async Task VerifyBranchAsync()
    {
        await RunBusyAsync("Verifying branch...", async () =>
        {
            var result = await branchVerificationService.VerifyAsync(Read());
            LogHub.AddResult("Branch verification", result);
            StatusMessage = result.Success ? "Branch exists" : string.Join(Environment.NewLine, result.Errors.Concat(result.Messages));
        });
    }

    [RelayCommand]
    private async Task TestDbAsync()
    {
        await RunBusyAsync("Testing database connection...", async () =>
        {
            var result = await testConnectionUseCase.ExecuteAsync(Read());
            LogHub.AddResult("Database connection", result);
            StatusMessage = result.Success ? "Database connection OK" : string.Join(Environment.NewLine, result.Errors);
        });
    }

    partial void OnApiBaseUrlChanged(string value)
    {
        connectivityMonitor.SetApiUrl(value);
    }

    private void Apply(AppSettings settings)
    {
        SqlInstance = settings.SqlInstance;
        SqlUser = settings.SqlUser;
        SqlPassword = settings.SqlPassword;
        ClientName = settings.ClientName;
        BranchCode = settings.BranchCode;
        PosNumber = settings.PosNumber;
        ApiBaseUrl = settings.ApiBaseUrl;
        BackupFolder = settings.BackupFolder;
        DbFilesPath = settings.DbFilesPath;
        Release = settings.Release;
        DatabasesText = string.Join(Environment.NewLine, settings.Databases);
        ServicesText = string.Join(Environment.NewLine, settings.Services);

        themeService.Apply(settings.Theme);
    }

    private AppSettings Read()
    {
        var settings = _settings.Clone();
        settings.SqlInstance = SqlInstance.Trim();
        settings.SqlUser = SqlUser.Trim();
        settings.SqlPassword = SqlPassword;
        settings.ClientName = ClientName.Trim();
        settings.BranchCode = BranchCode.Trim();
        settings.PosNumber = PosNumber.Trim();
        settings.ApiBaseUrl = ApiBaseUrl.Trim();
        settings.BackupFolder = BackupFolder.Trim();
        settings.DbFilesPath = DbFilesPath.Trim();
        settings.Release = Release.Trim();
        settings.Databases = MultilineText.SplitLines(DatabasesText);
        settings.Services = MultilineText.SplitLines(ServicesText);
        return settings;
    }

    private void OnConnectivityChanged(object? sender, bool connected)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            IsServerOnline = connected;
            ServerStatusText = connected ? "Online" : "Offline";
        });
    }

    public void Unload()
    {
        connectivityMonitor.StatusChanged -= OnConnectivityChanged;
    }
}
