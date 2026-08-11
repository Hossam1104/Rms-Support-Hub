using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using PosAdminTool.WinUI.Services;

namespace PosAdminTool.WinUI.ViewModels;

public sealed partial class ServicesViewModel : BaseViewModel
{
    private readonly IConfigurationService _configurationService;
    private readonly IServiceManager _serviceManager;
    private readonly LogHub _logHub;
    private DispatcherQueueTimer? _timer;

    public ServicesViewModel(IConfigurationService configurationService, IServiceManager serviceManager, LogHub logHub)
        : base(logHub)
    {
        _configurationService = configurationService;
        _serviceManager = serviceManager;
        _logHub = logHub;
    }

    public ObservableCollection<ServiceItemViewModel> Services { get; } = [];

    [RelayCommand]
    public async Task LoadAsync()
    {
        var settings = await _configurationService.LoadAsync();

        var existingNames = Services.Select(s => s.Name).ToList();
        if (!existingNames.SequenceEqual(settings.Services))
        {
            Services.Clear();
            foreach (var serviceName in settings.Services)
            {
                Services.Add(new ServiceItemViewModel(serviceName, _serviceManager, _logHub));
            }
        }

        await RefreshAsync();
        StartTimer();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        foreach (var service in Services)
        {
            await service.RefreshAsync();
        }
    }

    private void StartTimer()
    {
        if (_timer is not null)
        {
            return;
        }

        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(5);
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
    }

    public void StopTimer()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer = null;
        }
    }
}

public sealed partial class ServiceItemViewModel(
    string name,
    IServiceManager serviceManager,
    LogHub logHub)
    : ObservableObject
{
    [ObservableProperty]
    private ServiceStatus status = ServiceStatus.Unknown;

    [ObservableProperty]
    private bool isBusy;

    public string Name { get; } = name;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        Status = await serviceManager.GetStatusAsync(Name);
    }

    [RelayCommand]
    private Task StartAsync()
    {
        return ControlAsync(ServiceControlAction.Start);
    }

    [RelayCommand]
    private Task StopAsync()
    {
        return ControlAsync(ServiceControlAction.Stop);
    }

    [RelayCommand]
    private Task RestartAsync()
    {
        return ControlAsync(ServiceControlAction.Restart);
    }

    private async Task ControlAsync(ServiceControlAction action)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await serviceManager.ControlAsync(Name, action);
            await RefreshAsync();
            logHub.Add($"{action} completed for {Name}");
        }
        catch (Exception ex)
        {
            logHub.Add($"{action} failed for {Name}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
