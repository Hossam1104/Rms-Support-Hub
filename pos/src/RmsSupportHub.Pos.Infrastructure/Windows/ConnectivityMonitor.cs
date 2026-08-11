using System.Net.Sockets;
using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Infrastructure.Windows;

public sealed class ConnectivityMonitor : IConnectivityMonitor
{
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(3);
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private string _apiUrl = string.Empty;

    public event EventHandler<bool>? StatusChanged;

    public bool? LastStatus { get; private set; }

    public void SetApiUrl(string apiUrl)
    {
        _apiUrl = apiUrl ?? string.Empty;
        LastStatus = null;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_monitorTask is { IsCompleted: false })
            {
                return;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _monitorTask = Task.Run(() => MonitorAsync(_cts.Token), CancellationToken.None);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cts is null)
            {
                return;
            }

            await _cts.CancelAsync().ConfigureAwait(false);
            if (_monitorTask is not null)
            {
                try
                {
                    await _monitorTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (TimeoutException)
                {
                }
            }

            _cts.Dispose();
            _cts = null;
            _monitorTask = null;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _stateLock.Dispose();
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            var connected = await IsConnectedAsync(cancellationToken).ConfigureAwait(false);
            if (LastStatus is null || LastStatus.Value != connected)
            {
                LastStatus = connected;
                StatusChanged?.Invoke(this, connected);
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
    }

    private async Task<bool> IsConnectedAsync(CancellationToken cancellationToken)
    {
        var (host, port) = ParseHostPort(_apiUrl);
        if (string.IsNullOrWhiteSpace(host) || port is null)
        {
            return false;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);
            using var client = new TcpClient();
            await client.ConnectAsync(host, port.Value, timeoutCts.Token).ConfigureAwait(false);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public static (string? Host, int? Port) ParseHostPort(string apiUrl)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            return (null, null);
        }

        var candidate = apiUrl.Trim().TrimEnd('/');
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            var resolvedPort = uri.IsDefaultPort ? ResolveDefaultPort(uri.Scheme) : uri.Port;
            return (uri.Host, resolvedPort);
        }

        var withoutProtocol = candidate.Contains("://", StringComparison.Ordinal)
            ? candidate.Split("://", 2, StringSplitOptions.None)[1]
            : candidate;

        var pieces = withoutProtocol.Split(':', 2);
        if (pieces.Length == 2 && int.TryParse(pieces[1], out var explicitPort))
        {
            return (pieces[0], explicitPort);
        }

        return (withoutProtocol, 80);
    }

    private static int ResolveDefaultPort(string scheme)
    {
        return scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80;
    }
}
