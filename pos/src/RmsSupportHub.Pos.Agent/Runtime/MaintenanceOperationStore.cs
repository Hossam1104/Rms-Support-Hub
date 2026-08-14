using System.Runtime.CompilerServices;
using System.Threading.Channels;
using RmsSupportHub.Pos.Agent;
using RmsSupportHub.Pos.Contracts.V1.Maintenance;

namespace RmsSupportHub.Pos.Agent.Runtime;

/// <summary>Bounded, principal-scoped REST/SSE state for maintenance operations.</summary>
public sealed class MaintenanceOperationStore(
    TimeProvider clock,
    RuntimeRetentionPolicy retention)
{
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public MaintenanceOperationHandle Create(string principalSid, string mode, string correlationId)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var now = clock.GetUtcNow();
        var dto = new MaintenanceOperationDto(
            operationId,
            SafeMode(mode),
            MaintenanceOperationStateDto.Accepted,
            MaintenanceOperationStateDto.Accepted,
            0,
            "accepted",
            "The Agent accepted the typed maintenance operation.",
            now,
            null,
            new(false, false, [], [], []),
            null,
            SafeCorrelation(correlationId));

        lock (gate)
        {
            PruneLocked(now);
            if (entries.Count >= retention.MaxCompletedOperations)
            {
                throw new MaintenanceOperationCapacityException();
            }

            entries.Add(operationId, new(principalSid, dto));
        }

        return new(operationId, dto);
    }

    public bool TryGet(string principalSid, string operationId, out MaintenanceOperationDto? operation)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.TryGetValue(operationId, out var entry)
                && string.Equals(entry.PrincipalSid, principalSid, StringComparison.Ordinal))
            {
                operation = entry.Current;
                return true;
            }
        }

        operation = null;
        return false;
    }

    public bool Start(string operationId) => Update(operationId, current => current with
    {
        State = MaintenanceOperationStateDto.Running,
        Outcome = MaintenanceOperationStateDto.Accepted,
        ProgressPercent = Math.Max(1, current.ProgressPercent),
        Stage = "running",
        Detail = "The Agent is running the server-owned maintenance operation."
    });

    public bool Progress(
        string operationId,
        int progressPercent,
        string stage,
        string detail,
        MaintenanceOperationOutcomeDto? maintenanceOutcome = null) =>
        Update(operationId, current => current with
        {
            State = MaintenanceOperationStateDto.Running,
            Outcome = MaintenanceOperationStateDto.Accepted,
            ProgressPercent = Math.Clamp(progressPercent, 0, 100),
            Stage = SafeStage(stage),
            Detail = SafeDetail(detail),
            MaintenanceOutcome = maintenanceOutcome ?? current.MaintenanceOutcome
        }, ignoreFinal: true);

    public bool Complete(
        string operationId,
        MaintenanceOperationStateDto outcome,
        string code,
        string detail,
        MaintenanceOperationOutcomeDto maintenanceOutcome)
    {
        return Update(operationId, current => current with
        {
            State = outcome,
            Outcome = outcome,
            ProgressPercent = outcome == MaintenanceOperationStateDto.Completed ? 100 : current.ProgressPercent,
            Stage = outcome switch
            {
                MaintenanceOperationStateDto.Completed => "completed",
                MaintenanceOperationStateDto.Failed => "failed",
                MaintenanceOperationStateDto.OutcomeUnknown => "outcome-unknown",
                _ => "not-attempted"
            },
            Detail = SafeDetail(detail),
            CompletedAtUtc = clock.GetUtcNow(),
            MaintenanceOutcome = maintenanceOutcome,
            ErrorCode = SafeCode(code)
        }, completeSubscribers: true);
    }

    public async IAsyncEnumerable<MaintenanceOperationDto> StreamAsync(
        string principalSid,
        string operationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Entry? entry;
        Channel<MaintenanceOperationDto>? channel = null;
        MaintenanceOperationDto? initial = null;
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.TryGetValue(operationId, out entry)
                && string.Equals(entry.PrincipalSid, principalSid, StringComparison.Ordinal))
            {
                initial = entry.Current;
                if (!IsFinal(initial))
                {
                    channel = Channel.CreateBounded<MaintenanceOperationDto>(new BoundedChannelOptions(32)
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

        try
        {
            await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }
        }
        finally
        {
            lock (gate)
            {
                entry!.Subscribers.Remove(channel);
            }
        }
    }

    private bool Update(
        string operationId,
        Func<MaintenanceOperationDto, MaintenanceOperationDto> update,
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

    private static bool IsFinal(MaintenanceOperationDto operation) =>
        operation.State is MaintenanceOperationStateDto.NotAttempted
            or MaintenanceOperationStateDto.Completed
            or MaintenanceOperationStateDto.Failed
            or MaintenanceOperationStateDto.OutcomeUnknown;

    private static string SafeMode(string value) =>
        value is "cleanup" or "branch-reset" ? value : "maintenance";

    private static string SafeCorrelation(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(char.IsControl)
            ? "unavailable"
            : value;

    private static string SafeStage(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 64
            || value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))
            ? "running"
            : value;

    private static string SafeDetail(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 512 || value.Any(char.IsControl)
            ? "The Agent updated the maintenance operation state."
            : value;

    private static string SafeCode(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 96
            || value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))
            ? "maintenance_operation_completed"
            : value;

    private sealed class Entry(string principalSid, MaintenanceOperationDto current)
    {
        public string PrincipalSid { get; } = principalSid;
        public MaintenanceOperationDto Current { get; set; } = current;
        public List<Channel<MaintenanceOperationDto>> Subscribers { get; } = [];
    }
}

public sealed record MaintenanceOperationHandle(
    string OperationId,
    MaintenanceOperationDto InitialState);

public sealed class MaintenanceOperationCapacityException()
    : InvalidOperationException("The Agent maintenance operation retention limit has been reached.");
