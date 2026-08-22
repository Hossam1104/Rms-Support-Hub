using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.Desktop.Wpf.Services;

namespace RmsSupportHub.Pos.Desktop.Wpf.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged, IDisposable
{
    public static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromSeconds(30);

    private readonly ILocalAgentHealthClient healthClient;
    private readonly TimeSpan refreshInterval;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly CancellationTokenSource shutdown = new();
    private readonly AsyncCommand refreshCommand;
    private Task? refreshLoop;
    private bool disposed;
    private bool initialized;
    private bool isRefreshing;
    private HealthViewState state = HealthViewState.Loading;
    private string agentStatus = "Checking";
    private string ipcStatus = "Checking";
    private int? protocolVersion;
    private bool? hubConnectivityRequired;
    private string correlationId = string.Empty;
    private DateTimeOffset? lastSuccessfulCheck;
    private string errorCode = string.Empty;
    private string errorDetail = string.Empty;

    public DashboardViewModel(
        ILocalAgentHealthClient healthClient,
        TimeSpan? refreshInterval = null)
    {
        this.healthClient = healthClient ?? throw new ArgumentNullException(nameof(healthClient));
        this.refreshInterval = refreshInterval ?? DefaultRefreshInterval;
        if (this.refreshInterval < TimeSpan.FromSeconds(5)
            || this.refreshInterval > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(refreshInterval));
        }

        refreshCommand = new AsyncCommand(
            () => RefreshAsync(),
            () => !IsRefreshing && !disposed);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand RefreshCommand => refreshCommand;

    public HealthViewState State
    {
        get => state;
        private set => SetProperty(ref state, value);
    }

    public string AgentStatus
    {
        get => agentStatus;
        private set => SetProperty(ref agentStatus, value);
    }

    public string IpcStatus
    {
        get => ipcStatus;
        private set => SetProperty(ref ipcStatus, value);
    }

    public int? ProtocolVersion
    {
        get => protocolVersion;
        private set => SetProperty(ref protocolVersion, value);
    }

    public bool? HubConnectivityRequired
    {
        get => hubConnectivityRequired;
        private set => SetProperty(ref hubConnectivityRequired, value);
    }

    public string CorrelationId
    {
        get => correlationId;
        private set => SetProperty(ref correlationId, value);
    }

    public DateTimeOffset? LastSuccessfulCheck
    {
        get => lastSuccessfulCheck;
        private set
        {
            if (SetProperty(ref lastSuccessfulCheck, value))
            {
                OnPropertyChanged(nameof(LastSuccessfulCheckDisplay));
            }
        }
    }

    public string ErrorCode
    {
        get => errorCode;
        private set => SetProperty(ref errorCode, value);
    }

    public string ErrorDetail
    {
        get => errorDetail;
        private set => SetProperty(ref errorDetail, value);
    }

    public bool IsRefreshing
    {
        get => isRefreshing;
        private set
        {
            if (SetProperty(ref isRefreshing, value))
            {
                refreshCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanRefresh));
            }
        }
    }

    public bool CanRefresh => !IsRefreshing && !disposed;

    public bool IsAutomaticRefreshRunning => refreshLoop is { IsCompleted: false };

    public TimeSpan RefreshInterval => refreshInterval;

    public string StateLabel => State switch
    {
        HealthViewState.Loading => "Loading",
        HealthViewState.Connected => "Connected",
        HealthViewState.Unavailable => "Unavailable",
        HealthViewState.TimedOut => "Timed out",
        HealthViewState.ProtocolMismatch => "Version mismatch",
        HealthViewState.InvalidResponse => "Invalid response",
        HealthViewState.SecurityVerificationFailed => "Connection not verified",
        HealthViewState.UnknownError => "Health check failed",
        HealthViewState.Cancelled => "Cancelled",
        _ => "Unavailable"
    };

    public string StatusSummary => State switch
    {
        HealthViewState.Loading => "Checking the local Agent connection.",
        HealthViewState.Connected => "The local Agent is ready for supported operations.",
        HealthViewState.Unavailable => "RMS Support Agent is not available on this machine.",
        HealthViewState.TimedOut => "The Agent did not respond before the health check timed out.",
        HealthViewState.ProtocolMismatch => "The desktop and Agent protocol versions are not compatible.",
        HealthViewState.InvalidResponse => "The Agent returned an invalid health response.",
        HealthViewState.SecurityVerificationFailed => "The local Agent connection could not be verified.",
        HealthViewState.UnknownError => "The Agent health check could not be completed.",
        HealthViewState.Cancelled => "The health check was cancelled.",
        _ => "The Agent health check could not be completed."
    };

    public string ProtocolDisplay => ProtocolVersion is int version ? $"v{version}" : "—";

    public string HubRequiredDisplay => HubConnectivityRequired switch
    {
        true => "Yes",
        false => "No",
        _ => "—"
    };

    public string LastSuccessfulCheckDisplay => LastSuccessfulCheck is { } checkedAt
        ? checkedAt.ToLocalTime().ToString("HH:mm:ss")
        : "No successful check yet";

    public async Task InitializeAsync()
    {
        if (initialized || disposed)
        {
            return;
        }

        initialized = true;
        await RefreshAsync().ConfigureAwait(true);
        StartAutomaticRefresh();
    }

    public void StartAutomaticRefresh()
    {
        if (disposed || refreshLoop is { IsCompleted: false })
        {
            return;
        }

        refreshLoop = RunAutomaticRefreshAsync(shutdown.Token);
        OnPropertyChanged(nameof(IsAutomaticRefreshRunning));
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (disposed)
        {
            return;
        }

        try
        {
            if (!await refreshGate.WaitAsync(0, cancellationToken).ConfigureAwait(true))
            {
                return;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        IsRefreshing = true;
        State = HealthViewState.Loading;
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(StatusSummary));

        var requestCorrelationId = Guid.NewGuid().ToString("N");
        CorrelationId = requestCorrelationId;
        try
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                shutdown.Token,
                cancellationToken);
            var result = await healthClient
                .GetHealthAsync(requestCorrelationId, linkedCancellation.Token)
                .ConfigureAwait(true);

            if (linkedCancellation.IsCancellationRequested || disposed)
            {
                return;
            }

            ApplyResult(result);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
            if (!shutdown.IsCancellationRequested && !disposed)
            {
                ApplyResult(AgentHealthResult.Failure(
                    HealthViewState.Cancelled,
                    "cancelled",
                    "The health check was cancelled.",
                    requestCorrelationId));
            }
        }
        catch (Exception)
        {
            ApplyResult(AgentHealthResult.Failure(
                HealthViewState.UnknownError,
                "health_check_failed",
                "The Agent health check could not be completed.",
                requestCorrelationId));
        }
        finally
        {
            IsRefreshing = false;
            refreshGate.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        shutdown.Cancel();
        refreshCommand.RaiseCanExecuteChanged();
        shutdown.Dispose();
    }

    private async Task RunAutomaticRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(refreshInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(true))
            {
                await RefreshAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Window shutdown is an expected lifecycle event.
        }
        finally
        {
            OnPropertyChanged(nameof(IsAutomaticRefreshRunning));
        }
    }

    private void ApplyResult(AgentHealthResult result)
    {
        State = result.State;
        AgentStatus = result.AgentStatus ?? "Unavailable";
        IpcStatus = result.IpcStatus ?? "Unavailable";
        ProtocolVersion = result.ProtocolVersion;
        HubConnectivityRequired = result.HubConnectivityRequired;
        CorrelationId = result.CorrelationId ?? CorrelationId;
        ErrorCode = result.ErrorCode;
        ErrorDetail = result.ErrorDetail;
        if (result.IsConnected)
        {
            LastSuccessfulCheck = DateTimeOffset.Now;
        }

        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(ProtocolDisplay));
        OnPropertyChanged(nameof(HubRequiredDisplay));
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
