using System.Collections.Concurrent;

namespace RmsSupportHub.Pos.Agent.Services;

/// <summary>Non-blocking per-service gate preventing conflicting SCM calls.</summary>
public sealed class ServiceActionConcurrencyGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public ServiceActionLease? TryEnter(string serviceId)
    {
        var gate = _locks.GetOrAdd(serviceId, static _ => new SemaphoreSlim(1, 1));
        return gate.Wait(0) ? new ServiceActionLease(gate) : null;
    }
}

public sealed class ServiceActionLease(SemaphoreSlim gate) : IDisposable
{
    private int _released;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0)
        {
            gate.Release();
        }
    }
}
