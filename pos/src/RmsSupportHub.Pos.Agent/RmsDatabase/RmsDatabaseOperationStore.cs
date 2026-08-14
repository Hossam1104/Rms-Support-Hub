using System.Runtime.CompilerServices;
using System.Threading.Channels;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.RmsDatabase;

/// <summary>
/// Bounded process-local REST/SSE state for one Agent database operation. Every lookup is scoped to
/// the authenticated server-resolved SID; the SID itself is never part of the DTO.
/// </summary>
public sealed class RmsDatabaseOperationStore(
    TimeProvider clock,
    RuntimeRetentionPolicy retention)
{
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public RmsDatabaseOperationHandle Create(
        string principalSid,
        RmsDatabaseKind database,
        RmsDatabaseOperationKind operation,
        string correlationId)
    {
        var definition = RmsDatabaseCatalog.For(database);
        var operationId = Guid.NewGuid().ToString("N");
        var now = clock.GetUtcNow();
        var dto = new RmsDatabaseOperationDto(
            operationId,
            ToContractTarget(database),
            definition.DisplayName,
            operation,
            RmsDatabaseOperationState.Accepted,
            RmsDatabaseOperationOutcome.Accepted,
            0,
            "accepted",
            "The Agent accepted the typed database operation.",
            now,
            null,
            null,
            false,
            false,
            [],
            null,
            SafeCorrelation(correlationId));

        lock (gate)
        {
            PruneLocked(now);
            if (entries.Count >= retention.MaxCompletedOperations)
            {
                throw new RmsDatabaseOperationCapacityException();
            }

            entries.Add(operationId, new(principalSid, database, operation, dto));
        }

        return new(operationId, dto);
    }

    public bool TryGet(
        string principalSid,
        RmsDatabaseKind database,
        string operationId,
        out RmsDatabaseOperationDto? operation)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.TryGetValue(operationId, out var entry)
                && string.Equals(entry.PrincipalSid, principalSid, StringComparison.Ordinal)
                && entry.Database == database)
            {
                operation = entry.Current;
                return true;
            }
        }

        operation = null;
        return false;
    }

    public RmsDatabaseOperationDto? GetLatest(string principalSid, RmsDatabaseKind database)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            return entries.Values
                .Where(entry => string.Equals(entry.PrincipalSid, principalSid, StringComparison.Ordinal)
                    && entry.Database == database)
                .OrderByDescending(entry => entry.Current.StartedAtUtc)
                .Select(entry => entry.Current)
                .FirstOrDefault();
        }
    }

    public bool Start(string operationId)
    {
        return Update(operationId, current => current with
        {
            State = RmsDatabaseOperationState.Running,
            Outcome = RmsDatabaseOperationOutcome.Accepted,
            Stage = "running",
            Detail = "The Agent is running the server-owned database operation.",
            ProgressPercent = Math.Max(1, current.ProgressPercent)
        });
    }

    public bool Progress(string operationId, RmsDatabaseProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return Update(operationId, current => current with
        {
            State = RmsDatabaseOperationState.Running,
            Outcome = RmsDatabaseOperationOutcome.Accepted,
            ProgressPercent = Math.Clamp(progress.Percent, 0, 100),
            Stage = SafeStage(progress.Stage),
            Detail = SafeDetail(progress.Detail)
        }, ignoreFinal: true);
    }

    public bool Complete(
        string operationId,
        RmsDatabaseWorkflowOutcome outcome,
        string code,
        string detail,
        RmsApprovedDatabaseBackup? artifact,
        bool destructiveAttempted,
        bool recoveryRequired,
        IReadOnlyList<string> warnings)
    {
        var now = clock.GetUtcNow();
        var finalState = outcome switch
        {
            RmsDatabaseWorkflowOutcome.Completed => RmsDatabaseOperationState.Completed,
            RmsDatabaseWorkflowOutcome.Failed => RmsDatabaseOperationState.Failed,
            RmsDatabaseWorkflowOutcome.OutcomeUnknown => RmsDatabaseOperationState.OutcomeUnknown,
            _ => RmsDatabaseOperationState.NotAttempted
        };
        var finalOutcome = outcome switch
        {
            RmsDatabaseWorkflowOutcome.Completed => RmsDatabaseOperationOutcome.Completed,
            RmsDatabaseWorkflowOutcome.Failed => RmsDatabaseOperationOutcome.Failed,
            RmsDatabaseWorkflowOutcome.OutcomeUnknown => RmsDatabaseOperationOutcome.OutcomeUnknown,
            _ => RmsDatabaseOperationOutcome.NotAttempted
        };

        return Update(operationId, current => current with
        {
            State = finalState,
            Outcome = finalOutcome,
            ProgressPercent = outcome == RmsDatabaseWorkflowOutcome.Completed ? 100 : current.ProgressPercent,
            Stage = finalState switch
            {
                RmsDatabaseOperationState.Completed => "completed",
                RmsDatabaseOperationState.Failed => "failed",
                RmsDatabaseOperationState.OutcomeUnknown => "outcome-unknown",
                _ => "not-attempted"
            },
            Detail = SafeDetail(detail),
            CompletedAtUtc = now,
            Artifact = ToArtifactDto(artifact),
            DestructiveAttempted = destructiveAttempted,
            RecoveryRequired = recoveryRequired,
            Warnings = SanitizeWarnings(warnings),
            ErrorCode = SafeCode(code)
        }, completeSubscribers: true);
    }

    public async IAsyncEnumerable<RmsDatabaseOperationDto> StreamAsync(
        string principalSid,
        RmsDatabaseKind database,
        string operationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Entry? entry;
        Channel<RmsDatabaseOperationDto>? channel = null;
        RmsDatabaseOperationDto? initial = null;
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.TryGetValue(operationId, out entry)
                && string.Equals(entry.PrincipalSid, principalSid, StringComparison.Ordinal)
                && entry.Database == database)
            {
                initial = entry.Current;
                if (!IsFinal(initial))
                {
                    channel = Channel.CreateBounded<RmsDatabaseOperationDto>(new BoundedChannelOptions(32)
                    {
                        FullMode = BoundedChannelFullMode.DropOldest,
                        SingleReader = true,
                        SingleWriter = false
                    });
                    entry.Subscribers.Add(channel);
                }
            }
        }

        if (initial is null)
        {
            yield break;
        }

        yield return initial;
        if (channel is null)
        {
            yield break;
        }

        var subscriber = channel!;

        try
        {
            await foreach (var update in subscriber.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }
        }
        finally
        {
            lock (gate)
            {
                entry!.Subscribers.Remove(subscriber);
            }
        }
    }

    private bool Update(
        string operationId,
        Func<RmsDatabaseOperationDto, RmsDatabaseOperationDto> update,
        bool completeSubscribers = false,
        bool ignoreFinal = false)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(operationId, out var entry))
            {
                return false;
            }

            if (ignoreFinal && IsFinal(entry.Current))
            {
                return true;
            }

            entry.Current = update(entry.Current);
            foreach (var subscriber in entry.Subscribers.ToArray())
            {
                subscriber.Writer.TryWrite(entry.Current);
                if (completeSubscribers)
                {
                    subscriber.Writer.TryComplete();
                }
            }

            return true;
        }
    }

    private void PruneLocked(DateTimeOffset now)
    {
        foreach (var pair in entries.ToArray())
        {
            if (pair.Value.Subscribers.Count == 0
                && IsFinal(pair.Value.Current)
                && now - pair.Value.Current.CompletedAtUtc.GetValueOrDefault(pair.Value.Current.StartedAtUtc)
                    >= retention.CompletedOperationLifetime)
            {
                entries.Remove(pair.Key);
            }
        }
    }

    private static bool IsFinal(RmsDatabaseOperationDto operation) =>
        operation.State is RmsDatabaseOperationState.NotAttempted
            or RmsDatabaseOperationState.Completed
            or RmsDatabaseOperationState.Failed
            or RmsDatabaseOperationState.OutcomeUnknown;

    private static RmsDatabaseArtifactDto? ToArtifactDto(RmsApprovedDatabaseBackup? artifact) => artifact is null
        ? null
        : new(
            artifact.ArtifactId,
            artifact.DisplayName,
            artifact.SizeBytes,
            artifact.Sha256Checksum,
            artifact.CreatedAtUtc,
            artifact.ExpiresAtUtc);

    private static RmsDatabaseTarget ToContractTarget(RmsDatabaseKind database) => database switch
    {
        RmsDatabaseKind.Branch => RmsDatabaseTarget.Branch,
        RmsDatabaseKind.Cashier => RmsDatabaseTarget.Cashier,
        _ => throw new ArgumentOutOfRangeException(nameof(database))
    };

    private static string SafeCorrelation(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(char.IsControl)
            ? "unavailable"
            : value;

    private static string SafeStage(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 64 || value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))
            ? "running"
            : value;

    private static string SafeDetail(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 512 || value.Any(char.IsControl)
            ? "The Agent updated the database operation state."
            : value;

    private static string SafeCode(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 96 || value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))
            ? "database_operation_completed"
            : value;

    private static IReadOnlyList<string> SanitizeWarnings(IReadOnlyList<string> warnings) =>
        (warnings ?? [])
            .Where(warning => !string.IsNullOrWhiteSpace(warning) && warning.Length <= 512 && !warning.Any(char.IsControl))
            .Take(16)
            .ToArray();

    private sealed class Entry(
        string principalSid,
        RmsDatabaseKind database,
        RmsDatabaseOperationKind operation,
        RmsDatabaseOperationDto current)
    {
        public string PrincipalSid { get; } = principalSid;
        public RmsDatabaseKind Database { get; } = database;
        public RmsDatabaseOperationKind Operation { get; } = operation;
        public RmsDatabaseOperationDto Current { get; set; } = current;
        public List<Channel<RmsDatabaseOperationDto>> Subscribers { get; } = [];
    }
}

public sealed record RmsDatabaseOperationHandle(
    string OperationId,
    RmsDatabaseOperationDto InitialState);

public sealed class RmsDatabaseOperationCapacityException()
    : InvalidOperationException("The Agent operation retention limit has been reached.");
