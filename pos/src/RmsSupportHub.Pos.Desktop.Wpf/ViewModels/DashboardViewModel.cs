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
    private readonly ILocalServiceHealthClient serviceHealthClient;
    private readonly ILocalDatabaseHealthClient databaseHealthClient;
    private readonly TimeSpan refreshInterval;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly CancellationTokenSource shutdown = new();
    private readonly AsyncCommand refreshCommand;
    private readonly AsyncCommand showDashboardCommand;
    private readonly AsyncCommand showServicesCommand;
    private readonly AsyncCommand showDatabaseCommand;
    private Task? refreshLoop;
    private bool disposed;
    private bool initialized;
    private bool isRefreshing;
    private bool servicesWorkspace;
    private bool databaseWorkspace;
    private HealthViewState state = HealthViewState.Loading;
    private string agentStatus = "Checking";
    private string ipcStatus = "Checking";
    private int? protocolVersion;
    private bool? hubConnectivityRequired;
    private string correlationId = string.Empty;
    private DateTimeOffset? lastSuccessfulCheck;
    private string errorCode = string.Empty;
    private string errorDetail = string.Empty;
    private ServiceHealthViewState serviceState = ServiceHealthViewState.Loading;
    private IReadOnlyList<ServiceHealthRow> serviceItems = [];
    private DateTimeOffset? lastServiceCheck;
    private string serviceErrorCode = string.Empty;
    private string serviceErrorDetail = string.Empty;
    private DatabaseHealthViewState databaseState = DatabaseHealthViewState.Loading;
    private IReadOnlyList<DatabaseHealthRow> databaseItems = [];
    private DateTimeOffset? lastDatabaseCheck;
    private string databaseErrorCode = string.Empty;
    private string databaseErrorDetail = string.Empty;

    public DashboardViewModel(
        ILocalAgentHealthClient healthClient,
        TimeSpan? refreshInterval = null)
        : this(
            healthClient,
            new UnavailableServiceHealthClient(),
            new UnavailableDatabaseHealthClient(),
            refreshInterval)
    {
    }

    public DashboardViewModel(
        ILocalAgentHealthClient healthClient,
        ILocalServiceHealthClient serviceHealthClient,
        TimeSpan? refreshInterval = null)
        : this(
            healthClient,
            serviceHealthClient,
            new UnavailableDatabaseHealthClient(),
            refreshInterval)
    {
    }

    public DashboardViewModel(
        ILocalAgentHealthClient healthClient,
        ILocalServiceHealthClient serviceHealthClient,
        ILocalDatabaseHealthClient databaseHealthClient,
        TimeSpan? refreshInterval = null)
    {
        this.healthClient = healthClient ?? throw new ArgumentNullException(nameof(healthClient));
        this.serviceHealthClient = serviceHealthClient ?? throw new ArgumentNullException(nameof(serviceHealthClient));
        this.databaseHealthClient = databaseHealthClient ?? throw new ArgumentNullException(nameof(databaseHealthClient));
        this.refreshInterval = refreshInterval ?? DefaultRefreshInterval;
        if (this.refreshInterval < TimeSpan.FromSeconds(5)
            || this.refreshInterval > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(refreshInterval));
        }

        refreshCommand = new AsyncCommand(
            () => RefreshAsync(),
            () => !IsRefreshing && !disposed);
        showDashboardCommand = new AsyncCommand(
            ShowDashboardAsync,
            () => !disposed);
        showServicesCommand = new AsyncCommand(
            ShowServicesAsync,
            () => !disposed);
        showDatabaseCommand = new AsyncCommand(
            ShowDatabaseAsync,
            () => !disposed);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand RefreshCommand => refreshCommand;

    public ICommand ShowDashboardCommand => showDashboardCommand;

    public ICommand ShowServicesCommand => showServicesCommand;

    public ICommand ShowDatabaseCommand => showDatabaseCommand;

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

    public ServiceHealthViewState ServiceState
    {
        get => serviceState;
        private set => SetProperty(ref serviceState, value);
    }

    public IReadOnlyList<ServiceHealthRow> ServiceItems
    {
        get => serviceItems;
        private set => SetProperty(ref serviceItems, value);
    }

    public DateTimeOffset? LastServiceCheck
    {
        get => lastServiceCheck;
        private set
        {
            if (SetProperty(ref lastServiceCheck, value))
            {
                OnPropertyChanged(nameof(LastServiceCheckDisplay));
            }
        }
    }

    public string ServiceErrorCode
    {
        get => serviceErrorCode;
        private set => SetProperty(ref serviceErrorCode, value);
    }

    public string ServiceErrorDetail
    {
        get => serviceErrorDetail;
        private set => SetProperty(ref serviceErrorDetail, value);
    }

    public DatabaseHealthViewState DatabaseState
    {
        get => databaseState;
        private set => SetProperty(ref databaseState, value);
    }

    public IReadOnlyList<DatabaseHealthRow> DatabaseItems
    {
        get => databaseItems;
        private set => SetProperty(ref databaseItems, value);
    }

    public DateTimeOffset? LastDatabaseCheck
    {
        get => lastDatabaseCheck;
        private set
        {
            if (SetProperty(ref lastDatabaseCheck, value))
            {
                OnPropertyChanged(nameof(LastDatabaseCheckDisplay));
            }
        }
    }

    public string DatabaseErrorCode
    {
        get => databaseErrorCode;
        private set => SetProperty(ref databaseErrorCode, value);
    }

    public string DatabaseErrorDetail
    {
        get => databaseErrorDetail;
        private set => SetProperty(ref databaseErrorDetail, value);
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

    public bool IsDashboardVisible => !servicesWorkspace && !databaseWorkspace;

    public bool IsServicesVisible => servicesWorkspace;

    public bool IsDatabaseVisible => databaseWorkspace;

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

    public string ServiceStateLabel => ServiceState switch
    {
        ServiceHealthViewState.Loading => "Loading",
        ServiceHealthViewState.Healthy => "Healthy",
        ServiceHealthViewState.Degraded => "Degraded",
        ServiceHealthViewState.Unavailable => "Unavailable",
        ServiceHealthViewState.TimedOut => "Timed out",
        ServiceHealthViewState.ProtocolMismatch => "Version mismatch",
        ServiceHealthViewState.InvalidResponse => "Invalid response",
        ServiceHealthViewState.SecurityVerificationFailed => "Connection not verified",
        ServiceHealthViewState.Unknown => "Unknown",
        ServiceHealthViewState.UnknownError => "Unable to determine",
        ServiceHealthViewState.Cancelled => "Cancelled",
        _ => "Unavailable"
    };

    public string ServiceStatusSummary => ServiceState switch
    {
        ServiceHealthViewState.Loading => "Checking fixed RMS and Agent service identities.",
        ServiceHealthViewState.Healthy => "All fixed RMS and Agent services are running.",
        ServiceHealthViewState.Degraded => "One or more fixed services need attention.",
        ServiceHealthViewState.Unavailable => "Service health unavailable.",
        ServiceHealthViewState.TimedOut => "Service health check timed out.",
        ServiceHealthViewState.ProtocolMismatch => "Desktop and Agent versions are not compatible.",
        ServiceHealthViewState.InvalidResponse => "The Agent returned an invalid service health response.",
        ServiceHealthViewState.SecurityVerificationFailed => "The local Agent connection could not be verified.",
        ServiceHealthViewState.Unknown => "The Agent returned incomplete service status.",
        ServiceHealthViewState.UnknownError => "Service health could not be determined.",
        ServiceHealthViewState.Cancelled => "Service health check was cancelled.",
        _ => "Service health could not be determined."
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

    public string LastServiceCheckDisplay => LastServiceCheck is { } checkedAt
        ? checkedAt.ToLocalTime().ToString("HH:mm:ss")
        : "No successful check yet";

    public string LastDatabaseCheckDisplay => LastDatabaseCheck is { } checkedAt
        ? checkedAt.ToLocalTime().ToString("HH:mm:ss")
        : "No successful check yet";

    public int ServiceRunningCount => ServiceItems.Count(service => service.State == ServiceHealthRowState.Running);

    public int ServiceStoppedCount => ServiceItems.Count(service => service.State == ServiceHealthRowState.Stopped);

    public int ServiceUnknownCount => ServiceItems.Count(service => service.State is
        ServiceHealthRowState.Unknown
        or ServiceHealthRowState.Paused
        or ServiceHealthRowState.Transitioning);

    public string ServiceSummaryDisplay => ServiceState switch
    {
        ServiceHealthViewState.Healthy or ServiceHealthViewState.Degraded or ServiceHealthViewState.Unknown =>
            $"{ServiceRunningCount} Running  |  {ServiceStoppedCount} Stopped  |  {ServiceUnknownCount} Unknown",
        ServiceHealthViewState.Loading => "Checking",
        _ => "Unavailable"
    };

    public string ServiceSummaryDetail => ServiceState is
        ServiceHealthViewState.Healthy
        or ServiceHealthViewState.Degraded
        or ServiceHealthViewState.Unknown
        ? $"{ServiceItems.Count} fixed service identities"
        : ServiceStatusSummary;

    public string DatabaseStateLabel => DatabaseState switch
    {
        DatabaseHealthViewState.Loading => "Loading",
        DatabaseHealthViewState.Healthy => "Healthy",
        DatabaseHealthViewState.Degraded => "Degraded",
        DatabaseHealthViewState.Unavailable => "Unavailable",
        DatabaseHealthViewState.TimedOut => "Timed out",
        DatabaseHealthViewState.ProtocolMismatch => "Version mismatch",
        DatabaseHealthViewState.InvalidResponse => "Invalid response",
        DatabaseHealthViewState.SecurityVerificationFailed => "Connection not verified",
        DatabaseHealthViewState.Unknown => "Unknown",
        DatabaseHealthViewState.UnknownError => "Unable to determine",
        DatabaseHealthViewState.Cancelled => "Cancelled",
        _ => "Unavailable"
    };

    public string DatabaseStatusSummary => DatabaseState switch
    {
        DatabaseHealthViewState.Loading => "Checking the fixed Branch and Cashier database identities.",
        DatabaseHealthViewState.Healthy => "Both canonical RMS databases answered the identity probe.",
        DatabaseHealthViewState.Degraded => "One or more canonical databases need attention.",
        DatabaseHealthViewState.Unavailable => "RMS database health is currently unavailable.",
        DatabaseHealthViewState.TimedOut => "Database health check timed out.",
        DatabaseHealthViewState.ProtocolMismatch => "Desktop and Agent versions are not compatible.",
        DatabaseHealthViewState.InvalidResponse => "The Agent returned an invalid database health response.",
        DatabaseHealthViewState.SecurityVerificationFailed => "The local Agent connection could not be verified.",
        DatabaseHealthViewState.Unknown => "The Agent returned incomplete database health.",
        DatabaseHealthViewState.UnknownError => "Database health could not be determined.",
        DatabaseHealthViewState.Cancelled => "Database health check was cancelled.",
        _ => "Database health could not be determined."
    };

    public string DatabaseSummaryDisplay => DatabaseState switch
    {
        DatabaseHealthViewState.Healthy or DatabaseHealthViewState.Degraded or DatabaseHealthViewState.Unknown =>
            $"{DatabaseConnectedCount} Connected  |  {DatabaseAttentionCount} Attention",
        DatabaseHealthViewState.Loading => "Checking",
        _ => "Unavailable"
    };

    public string DatabaseSummaryDetail => DatabaseState is
        DatabaseHealthViewState.Healthy
        or DatabaseHealthViewState.Degraded
        or DatabaseHealthViewState.Unknown
        ? "Branch and Cashier database identity probes"
        : DatabaseStatusSummary;

    public int DatabaseConnectedCount => DatabaseItems.Count(database => database.IsConnected);

    public int DatabaseAttentionCount => DatabaseItems.Count(database => !database.IsConnected);

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
        ServiceState = ServiceHealthViewState.Loading;
        DatabaseState = DatabaseHealthViewState.Loading;
        NotifyAgentStateChanged();
        NotifyServiceStateChanged();
        NotifyDatabaseStateChanged();

        var requestCorrelationId = Guid.NewGuid().ToString("N");
        CorrelationId = requestCorrelationId;
        try
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                shutdown.Token,
                cancellationToken);
            AgentHealthResult agentResult;
            try
            {
                agentResult = await healthClient
                    .GetHealthAsync(requestCorrelationId, linkedCancellation.Token)
                    .ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
            {
                if (!shutdown.IsCancellationRequested && !disposed)
                {
                    ApplyResult(AgentHealthResult.Failure(
                        HealthViewState.Cancelled,
                        "cancelled",
                        "The health check was cancelled.",
                        requestCorrelationId));
                    ApplyServiceResult(ServiceHealthResult.Failure(
                        ServiceHealthViewState.Cancelled,
                        "cancelled",
                        "Service health check was cancelled.",
                        requestCorrelationId));
                    ApplyDatabaseResult(DatabaseHealthResult.Failure(
                        DatabaseHealthViewState.Cancelled,
                        "cancelled",
                        "Database health check was cancelled.",
                        requestCorrelationId));
                }

                return;
            }
            catch (Exception)
            {
                agentResult = AgentHealthResult.Failure(
                    HealthViewState.UnknownError,
                    "health_check_failed",
                    "The Agent health check could not be completed.",
                    requestCorrelationId);
            }

            if (linkedCancellation.IsCancellationRequested || disposed)
            {
                return;
            }

            ApplyResult(agentResult);
            if (!agentResult.IsConnected)
            {
                ApplyServiceResult(ServiceHealthResult.Failure(
                    ServiceHealthViewState.Unavailable,
                    "agent_unavailable",
                    "RMS Support Agent is not available on this machine.",
                    requestCorrelationId));
                ApplyDatabaseResult(DatabaseHealthResult.Failure(
                    DatabaseHealthViewState.Unavailable,
                    "agent_unavailable",
                    "RMS Support Agent is not available on this machine.",
                    requestCorrelationId));
                return;
            }

            ServiceHealthResult serviceResult;
            try
            {
                serviceResult = await serviceHealthClient
                    .GetHealthAsync(requestCorrelationId, linkedCancellation.Token)
                    .ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
            {
                if (!shutdown.IsCancellationRequested && !disposed)
                {
                    ApplyServiceResult(ServiceHealthResult.Failure(
                        ServiceHealthViewState.Cancelled,
                        "cancelled",
                        "Service health check was cancelled.",
                        requestCorrelationId));
                    ApplyDatabaseResult(DatabaseHealthResult.Failure(
                        DatabaseHealthViewState.Cancelled,
                        "cancelled",
                        "Database health check was cancelled.",
                        requestCorrelationId));
                }

                return;
            }
            catch (Exception)
            {
                serviceResult = ServiceHealthResult.Failure(
                    ServiceHealthViewState.UnknownError,
                    "service_health_failed",
                    "Service health could not be determined.",
                    requestCorrelationId);
            }

            if (linkedCancellation.IsCancellationRequested || disposed)
            {
                return;
            }

            ApplyServiceResult(serviceResult);

            DatabaseHealthResult databaseResult;
            try
            {
                databaseResult = await databaseHealthClient
                    .GetHealthAsync(requestCorrelationId, linkedCancellation.Token)
                    .ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
            {
                if (!shutdown.IsCancellationRequested && !disposed)
                {
                    ApplyDatabaseResult(DatabaseHealthResult.Failure(
                        DatabaseHealthViewState.Cancelled,
                        "cancelled",
                        "Database health check was cancelled.",
                        requestCorrelationId));
                }

                return;
            }
            catch (Exception)
            {
                databaseResult = DatabaseHealthResult.Failure(
                    DatabaseHealthViewState.UnknownError,
                    "database_health_failed",
                    "Database health could not be determined.",
                    requestCorrelationId);
            }

            if (linkedCancellation.IsCancellationRequested || disposed)
            {
                return;
            }

            ApplyDatabaseResult(databaseResult);
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
        showDashboardCommand.RaiseCanExecuteChanged();
        showServicesCommand.RaiseCanExecuteChanged();
        showDatabaseCommand.RaiseCanExecuteChanged();
        shutdown.Dispose();
    }

    private Task ShowDashboardAsync()
    {
        if (disposed)
        {
            return Task.CompletedTask;
        }

        servicesWorkspace = false;
        databaseWorkspace = false;
        OnPropertyChanged(nameof(IsDashboardVisible));
        OnPropertyChanged(nameof(IsServicesVisible));
        OnPropertyChanged(nameof(IsDatabaseVisible));
        return Task.CompletedTask;
    }

    private Task ShowServicesAsync()
    {
        if (disposed)
        {
            return Task.CompletedTask;
        }

        servicesWorkspace = true;
        databaseWorkspace = false;
        OnPropertyChanged(nameof(IsDashboardVisible));
        OnPropertyChanged(nameof(IsServicesVisible));
        OnPropertyChanged(nameof(IsDatabaseVisible));
        return Task.CompletedTask;
    }

    private Task ShowDatabaseAsync()
    {
        if (disposed)
        {
            return Task.CompletedTask;
        }

        servicesWorkspace = false;
        databaseWorkspace = true;
        OnPropertyChanged(nameof(IsDashboardVisible));
        OnPropertyChanged(nameof(IsServicesVisible));
        OnPropertyChanged(nameof(IsDatabaseVisible));
        return Task.CompletedTask;
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

        NotifyAgentStateChanged();
    }

    private void ApplyServiceResult(ServiceHealthResult result)
    {
        ServiceState = result.State;
        ServiceItems = result.Services;
        ServiceErrorCode = result.ErrorCode;
        ServiceErrorDetail = result.ErrorDetail;
        if (result.CheckedAtUtc is { } checkedAtUtc)
        {
            LastServiceCheck = checkedAtUtc;
        }

        NotifyServiceStateChanged();
    }

    private void ApplyDatabaseResult(DatabaseHealthResult result)
    {
        DatabaseState = result.State;
        DatabaseItems = result.Databases;
        DatabaseErrorCode = result.ErrorCode;
        DatabaseErrorDetail = result.ErrorDetail;
        if (result.CheckedAtUtc is { } checkedAtUtc)
        {
            LastDatabaseCheck = checkedAtUtc;
        }

        NotifyDatabaseStateChanged();
    }

    private void NotifyAgentStateChanged()
    {
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(ProtocolDisplay));
        OnPropertyChanged(nameof(HubRequiredDisplay));
    }

    private void NotifyServiceStateChanged()
    {
        OnPropertyChanged(nameof(ServiceStateLabel));
        OnPropertyChanged(nameof(ServiceStatusSummary));
        OnPropertyChanged(nameof(ServiceRunningCount));
        OnPropertyChanged(nameof(ServiceStoppedCount));
        OnPropertyChanged(nameof(ServiceUnknownCount));
        OnPropertyChanged(nameof(ServiceSummaryDisplay));
        OnPropertyChanged(nameof(ServiceSummaryDetail));
    }

    private void NotifyDatabaseStateChanged()
    {
        OnPropertyChanged(nameof(DatabaseStateLabel));
        OnPropertyChanged(nameof(DatabaseStatusSummary));
        OnPropertyChanged(nameof(DatabaseSummaryDisplay));
        OnPropertyChanged(nameof(DatabaseSummaryDetail));
        OnPropertyChanged(nameof(DatabaseConnectedCount));
        OnPropertyChanged(nameof(DatabaseAttentionCount));
    }

    private bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
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

    private sealed class UnavailableServiceHealthClient : ILocalServiceHealthClient
    {
        public Task<ServiceHealthResult> GetHealthAsync(
            string correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceHealthResult.Failure(
                ServiceHealthViewState.Unavailable,
                "agent_unavailable",
                "RMS Support Agent is not available on this machine.",
                correlationId));
    }

    private sealed class UnavailableDatabaseHealthClient : ILocalDatabaseHealthClient
    {
        public Task<DatabaseHealthResult> GetHealthAsync(
            string correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DatabaseHealthResult.Failure(
                DatabaseHealthViewState.Unavailable,
                "agent_unavailable",
                "RMS Support Agent is not available on this machine.",
                correlationId));
    }
}
