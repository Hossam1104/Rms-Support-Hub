using RmsSupportHub.Pos.Agent;

namespace RmsSupportHub.Pos.Agent.Runtime;

public enum AgentIdempotencyReservationState
{
    Reserved,
    InProgress,
    Completed,
    Conflict,
    Capacity
}

public readonly record struct AgentIdempotencyReservation(
    AgentIdempotencyReservationState State,
    string? OperationId = null);

/// <summary>
/// Bounded idempotency for downloader requests. The request material is retained so a reused key
/// cannot silently become a different branch dispatch.
/// </summary>
public sealed class DownloaderIdempotencyStore(
    RuntimeRetentionPolicy retention,
    TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Dictionary<Key, Entry> entries = new();

    public AgentIdempotencyReservation TryReserve(
        string principalSid,
        string idempotencyKey,
        string material)
    {
        var key = new Key(principalSid, idempotencyKey);
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

            entries.Add(key, new(material, clock.GetUtcNow(), null));
            return new(AgentIdempotencyReservationState.Reserved);
        }
    }

    public void Bind(string principalSid, string idempotencyKey, string operationId)
    {
        lock (gate)
        {
            var key = new Key(principalSid, idempotencyKey);
            if (entries.TryGetValue(key, out var existing))
            {
                entries[key] = existing with { OperationId = operationId };
            }
        }
    }

    public void Release(string principalSid, string idempotencyKey)
    {
        lock (gate)
        {
            var key = new Key(principalSid, idempotencyKey);
            if (entries.TryGetValue(key, out var existing) && existing.OperationId is null)
            {
                entries.Remove(key);
            }
        }
    }

    public static bool IsValidKey(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(character => character is >= '!' and <= '~');

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

    private readonly record struct Key(string PrincipalSid, string IdempotencyKey);
    private sealed record Entry(string Material, DateTimeOffset CreatedAtUtc, string? OperationId);
}

/// <summary>Bounded idempotency for cleanup and branch-reset operations.</summary>
public sealed class MaintenanceIdempotencyStore(
    RuntimeRetentionPolicy retention,
    TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Dictionary<Key, Entry> entries = new();

    public AgentIdempotencyReservation TryReserve(
        string principalSid,
        string mode,
        string idempotencyKey,
        string material)
    {
        var key = new Key(principalSid, mode, idempotencyKey);
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

            entries.Add(key, new(material, clock.GetUtcNow(), null));
            return new(AgentIdempotencyReservationState.Reserved);
        }
    }

    public void Bind(string principalSid, string mode, string idempotencyKey, string operationId)
    {
        lock (gate)
        {
            var key = new Key(principalSid, mode, idempotencyKey);
            if (entries.TryGetValue(key, out var existing))
            {
                entries[key] = existing with { OperationId = operationId };
            }
        }
    }

    public void Release(string principalSid, string mode, string idempotencyKey)
    {
        lock (gate)
        {
            var key = new Key(principalSid, mode, idempotencyKey);
            if (entries.TryGetValue(key, out var existing) && existing.OperationId is null)
            {
                entries.Remove(key);
            }
        }
    }

    public static bool IsValidKey(string? value) => DownloaderIdempotencyStore.IsValidKey(value);

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

    private readonly record struct Key(string PrincipalSid, string Mode, string IdempotencyKey);
    private sealed record Entry(string Material, DateTimeOffset CreatedAtUtc, string? OperationId);
}
