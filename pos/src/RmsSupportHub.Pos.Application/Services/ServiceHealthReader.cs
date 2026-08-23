using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Services;

/// <summary>
/// Shared bounded read of the server-owned RMS and Agent service catalog. This class contains no
/// transport or caller authorization logic; transports enter through the typed query handler.
/// </summary>
public sealed class ServiceHealthReader
{
    public static readonly TimeSpan DefaultLookupTimeout = TimeSpan.FromSeconds(5);

    private readonly IServiceManager serviceManager;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan lookupTimeout;

    public ServiceHealthReader(
        IServiceManager serviceManager,
        TimeProvider timeProvider,
        TimeSpan? lookupTimeout = null)
    {
        this.serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.lookupTimeout = lookupTimeout ?? DefaultLookupTimeout;
        if (this.lookupTimeout <= TimeSpan.Zero || this.lookupTimeout > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(nameof(lookupTimeout));
        }
    }

    public async Task<ServiceHealthSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var definitions = ServiceHealthCatalog.Definitions;
        if (definitions.Count == 0)
        {
            return new(
                ServiceHealthOverallState.Unknown,
                timeProvider.GetUtcNow(),
                []);
        }

        using var lookupTimeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lookupTimeoutCancellation.CancelAfter(lookupTimeout);

        IReadOnlyDictionary<string, ServiceStatus> statuses;
        try
        {
            statuses = await serviceManager
                .GetStatusesAsync(
                    definitions.Select(definition => definition.ServiceName),
                    lookupTimeoutCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ServiceHealthLookupTimeoutException();
        }

        var checkedAtUtc = timeProvider.GetUtcNow();
        var items = definitions
            .Select(definition =>
            {
                var state = statuses.GetValueOrDefault(definition.ServiceName, ServiceStatus.Unknown);
                return new ServiceHealthItem(
                    definition.ServiceName,
                    ServiceIdentityCatalog.ToServiceId(definition.ServiceName),
                    definition.DisplayName,
                    definition.Required,
                    state,
                    ToSafeStatusCode(state));
            })
            .ToArray();

        return new(DetermineOverallState(items), checkedAtUtc, items);
    }

    public static ServiceHealthOverallState DetermineOverallState(
        IReadOnlyList<ServiceHealthItem> items)
    {
        if (items.Count == 0 || items.Any(item => item.RuntimeState == ServiceStatus.Unknown))
        {
            return ServiceHealthOverallState.Unknown;
        }

        return items.Any(item => item.Required && item.RuntimeState != ServiceStatus.Running)
            ? ServiceHealthOverallState.Degraded
            : ServiceHealthOverallState.Healthy;
    }

    public static string ToSafeStatusCode(ServiceStatus state) => state switch
    {
        ServiceStatus.Running => "running",
        ServiceStatus.Stopped => "stopped",
        ServiceStatus.Paused => "paused",
        ServiceStatus.Transitioning => "transitioning",
        ServiceStatus.NotFound => "not_installed",
        _ => "unknown"
    };
}

public sealed class ServiceHealthLookupTimeoutException()
    : TimeoutException("The bounded service health lookup timed out.");
