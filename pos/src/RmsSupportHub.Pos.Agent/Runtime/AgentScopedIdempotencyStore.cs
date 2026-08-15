namespace RmsSupportHub.Pos.Agent.Runtime;

/// <summary>Small, bounded idempotency store shared by the new typed Slice B operations.</summary>
public sealed class AgentScopedIdempotencyStore(
    RuntimeRetentionPolicy retention,
    TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Dictionary<Key, Entry> entries = new();

    public static bool IsValidKey(string? value) => DownloaderIdempotencyStore.IsValidKey(value);

    public AgentIdempotencyReservation TryReserve(
        string principalSid,
        string operationScope,
        string idempotencyKey,
        string material)
    {
        if (!DownloaderIdempotencyStore.IsValidKey(idempotencyKey))
        {
            return new(AgentIdempotencyReservationState.Conflict);
        }

        var key = new Key(principalSid, operationScope, idempotencyKey);
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.TryGetValue(key, out var existing))
            {
                if (!string.Equals(existing.Material, material, StringComparison.Ordinal))
                {
                    return new(AgentIdempotencyReservationState.Conflict);
                }

                return existing.OperationId is null
                    ? new(AgentIdempotencyReservationState.InProgress)
                    : new(AgentIdempotencyReservationState.Completed, existing.OperationId);
            }

            if (entries.Count >= retention.MaxCompletedOperations)
            {
                return new(AgentIdempotencyReservationState.Capacity);
            }

            entries[key] = new(material, clock.GetUtcNow(), null);
            return new(AgentIdempotencyReservationState.Reserved);
        }
    }

    public void Bind(string principalSid, string operationScope, string idempotencyKey, string operationId)
    {
        lock (gate)
        {
            var key = new Key(principalSid, operationScope, idempotencyKey);
            if (entries.TryGetValue(key, out var existing))
            {
                entries[key] = existing with { OperationId = operationId };
            }
        }
    }

    public void Release(string principalSid, string operationScope, string idempotencyKey)
    {
        lock (gate)
        {
            var key = new Key(principalSid, operationScope, idempotencyKey);
            if (entries.TryGetValue(key, out var existing) && existing.OperationId is null)
            {
                entries.Remove(key);
            }
        }
    }

    private void PruneLocked(DateTimeOffset now)
    {
        foreach (var pair in entries.ToArray())
        {
            if (now - pair.Value.CreatedAtUtc >= retention.CompletedOperationLifetime)
            {
                entries.Remove(pair.Key);
            }
        }
    }

    private readonly record struct Key(string PrincipalSid, string OperationScope, string IdempotencyKey);
    private sealed record Entry(string Material, DateTimeOffset CreatedAtUtc, string? OperationId);
}
