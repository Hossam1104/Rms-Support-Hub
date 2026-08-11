namespace RmsSupportHub.Pos.Domain.Interfaces;

public interface IConnectivityMonitor : IAsyncDisposable
{
    event EventHandler<bool>? StatusChanged;

    bool? LastStatus { get; }

    void SetApiUrl(string apiUrl);

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
