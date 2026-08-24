namespace RmsSupportHub.Pos.Agent.Artifacts;

/// <summary>
/// Reference-counted, non-blocking keyed coordination. Idle keys are removed synchronously so
/// one-time destination names cannot grow a process-lifetime registry.
/// </summary>
public sealed class BoundedKeyedMutationCoordinator
{
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);

    public bool TryEnter(string key, out IDisposable lease)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                entry = new();
                entries.Add(key, entry);
            }

            entry.ReferenceCount++;
            if (!entry.Semaphore.Wait(0))
            {
                ReleaseReferenceLocked(key, entry, permitHeld: false);
                lease = null!;
                return false;
            }

            lease = new Lease(this, key, entry);
            return true;
        }
    }

    public int ActiveKeyCount
    {
        get
        {
            lock (gate) return entries.Count;
        }
    }

    private void Release(string key, Entry entry)
    {
        lock (gate)
        {
            ReleaseReferenceLocked(key, entry, permitHeld: true);
        }
    }

    private void ReleaseReferenceLocked(string key, Entry entry, bool permitHeld)
    {
        if (permitHeld)
        {
            entry.Semaphore.Release();
        }

        entry.ReferenceCount--;
        if (entry.ReferenceCount == 0
            && entries.TryGetValue(key, out var current)
            && ReferenceEquals(current, entry))
        {
            entries.Remove(key);
            entry.Semaphore.Dispose();
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int ReferenceCount { get; set; }
    }

    private sealed class Lease(
        BoundedKeyedMutationCoordinator owner,
        string key,
        Entry entry) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                owner.Release(key, entry);
            }
        }
    }
}
