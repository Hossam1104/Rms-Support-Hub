using System.Collections.Concurrent;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.RmsDatabase;

public sealed class RmsDatabaseConcurrencyGate
{
    private readonly ConcurrentDictionary<RmsDatabaseKind, SemaphoreSlim> gates = new();

    public RmsDatabaseLease? TryEnter(RmsDatabaseKind database)
    {
        var gate = gates.GetOrAdd(database, static _ => new SemaphoreSlim(1, 1));
        return gate.Wait(0) ? new RmsDatabaseLease(gate) : null;
    }
}

public sealed class RmsDatabaseLease(SemaphoreSlim gate) : IDisposable
{
    private int released;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref released, 1) == 0)
        {
            gate.Release();
        }
    }
}

public enum RmsDatabaseReservationState
{
    Reserved,
    InProgress,
    Completed,
    Conflict,
    Capacity
}

public readonly record struct RmsDatabaseReservation(
    RmsDatabaseReservationState State,
    string? OperationId = null);

/// <summary>
/// Bounded idempotency keys for the typed database operations. Keys are scoped to the resolved
/// principal, canonical target, and operation kind; no caller-selected database/path participates.
/// </summary>
public sealed class RmsDatabaseIdempotencyStore(
    RuntimeRetentionPolicy retention,
    TimeProvider clock)
{
    private const int MaxKeyLength = 128;
    private readonly object gate = new();
    private readonly Dictionary<EntryKey, Entry> entries = new();

    public RmsDatabaseReservation TryReserve(
        string principalSid,
        RmsDatabaseKind database,
        RmsDatabaseOperationKind operation,
        string idempotencyKey)
    {
        var key = new EntryKey(principalSid, database, operation, idempotencyKey);
        var now = clock.GetUtcNow();
        lock (gate)
        {
            PruneLocked(now);
            if (entries.TryGetValue(key, out var existing))
            {
                return existing.OperationId is not null
                    ? new(RmsDatabaseReservationState.Completed, existing.OperationId)
                    : new(RmsDatabaseReservationState.InProgress);
            }

            if (entries.Count >= retention.MaxCompletedOperations)
            {
                return new(RmsDatabaseReservationState.Capacity);
            }

            entries.Add(key, new(now, null));
            return new(RmsDatabaseReservationState.Reserved);
        }
    }

    public void BindOperation(
        string principalSid,
        RmsDatabaseKind database,
        RmsDatabaseOperationKind operation,
        string idempotencyKey,
        string operationId)
    {
        var key = new EntryKey(principalSid, database, operation, idempotencyKey);
        lock (gate)
        {
            if (entries.TryGetValue(key, out var existing))
            {
                entries[key] = existing with { OperationId = operationId };
            }
        }
    }

    public void Release(
        string principalSid,
        RmsDatabaseKind database,
        RmsDatabaseOperationKind operation,
        string idempotencyKey)
    {
        var key = new EntryKey(principalSid, database, operation, idempotencyKey);
        lock (gate)
        {
            if (entries.TryGetValue(key, out var existing) && existing.OperationId is null)
            {
                entries.Remove(key);
            }
        }
    }

    public static bool IsValidKey(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaxKeyLength
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

    private readonly record struct EntryKey(
        string PrincipalSid,
        RmsDatabaseKind Database,
        RmsDatabaseOperationKind Operation,
        string IdempotencyKey);

    private sealed record Entry(DateTimeOffset CreatedAtUtc, string? OperationId);
}
