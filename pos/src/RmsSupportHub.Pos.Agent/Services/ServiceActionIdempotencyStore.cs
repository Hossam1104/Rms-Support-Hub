using RmsSupportHub.Pos.Contracts.V1.Services;

namespace RmsSupportHub.Pos.Agent.Services;

public enum ServiceActionReservationState
{
    Reserved,
    Completed,
    InProgress,
    Conflict,
    Capacity
}

public readonly record struct ServiceActionReservation(
    ServiceActionReservationState State,
    ServiceActionResponseDto? ExistingResponse = null);

/// <summary>
/// Bounded process-local idempotency state. Keys are scoped only to the opaque service ID and
/// caller key; Windows SIDs, usernames, Hub session IDs, and tokens are never used as identities.
/// </summary>
public sealed class ServiceActionIdempotencyStore
{
    private readonly object _gate = new();
    private readonly ServiceActionOptions _options;
    private readonly TimeProvider _clock;
    private readonly Dictionary<EntryKey, Entry> _entries = new();

    public ServiceActionIdempotencyStore(ServiceActionOptions options, TimeProvider clock)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public ServiceActionReservation TryReserve(
        string serviceId,
        string idempotencyKey,
        ServiceActionKind action)
    {
        var key = new EntryKey(serviceId, idempotencyKey);
        var now = _clock.GetUtcNow();

        lock (_gate)
        {
            PruneLocked(now);

            if (_entries.TryGetValue(key, out var existing))
            {
                if (existing.Action != action)
                {
                    return new(ServiceActionReservationState.Conflict);
                }

                return existing.Response is { } response
                    ? new(ServiceActionReservationState.Completed, response)
                    : new(ServiceActionReservationState.InProgress);
            }

            if (_entries.Count >= _options.MaxIdempotencyEntries)
            {
                return new(ServiceActionReservationState.Capacity);
            }

            _entries.Add(key, new Entry(action, now, null));
            return new(ServiceActionReservationState.Reserved);
        }
    }

    public void Complete(
        string serviceId,
        string idempotencyKey,
        ServiceActionKind action,
        ServiceActionResponseDto response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var key = new EntryKey(serviceId, idempotencyKey);

        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing) && existing.Action == action)
            {
                _entries[key] = existing with { Response = response };
            }
        }
    }

    public void Release(
        string serviceId,
        string idempotencyKey,
        ServiceActionKind action)
    {
        var key = new EntryKey(serviceId, idempotencyKey);

        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing)
                && existing.Action == action
                && existing.Response is null)
            {
                _entries.Remove(key);
            }
        }
    }

    private void PruneLocked(DateTimeOffset now)
    {
        foreach (var pair in _entries.ToArray())
        {
            if (now - pair.Value.CreatedAtUtc >= _options.IdempotencyRetention)
            {
                _entries.Remove(pair.Key);
            }
        }
    }

    private readonly record struct EntryKey(string ServiceId, string IdempotencyKey);

    private sealed record Entry(
        ServiceActionKind Action,
        DateTimeOffset CreatedAtUtc,
        ServiceActionResponseDto? Response);
}
