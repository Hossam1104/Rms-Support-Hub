using System.Runtime.CompilerServices;
using System.Threading.Channels;
using RmsSupportHub.Pos.Agent;
using RmsSupportHub.Pos.Contracts.V1.Downloader;

namespace RmsSupportHub.Pos.Agent.Runtime;

/// <summary>Bounded, principal-scoped REST/SSE state for downloader operations.</summary>
public sealed class DownloaderOperationStore(
    TimeProvider clock,
    RuntimeRetentionPolicy retention)
{
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public DownloaderOperationHandle Create(
        string principalSid,
        IReadOnlyList<string> branchCodes,
        string correlationId)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var now = clock.GetUtcNow();
        var branches = branchCodes
            .Select(branch => new DownloaderBranchOutcomeDto(branch, DownloaderBranchState.Pending, 0))
            .ToArray();
        var outcome = new DownloaderOperationOutcomeDto(
            branches,
            null,
            DownloaderTriggerStateDto.NotAttempted);
        var dto = new DownloaderOperationDto(
            operationId,
            DownloaderOperationStateDto.Accepted,
            DownloaderOperationStateDto.Accepted,
            0,
            "accepted",
            "The Agent accepted the typed downloader operation.",
            now,
            null,
            outcome,
            null,
            SafeCorrelation(correlationId));

        lock (gate)
        {
            PruneLocked(now);
            if (entries.Count >= retention.MaxCompletedOperations)
            {
                throw new DownloaderOperationCapacityException();
            }

            entries.Add(operationId, new(principalSid, dto));
        }

        return new(operationId, dto);
    }

    public bool TryGet(string principalSid, string operationId, out DownloaderOperationDto? operation)
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
        State = DownloaderOperationStateDto.Running,
        Outcome = DownloaderOperationStateDto.Accepted,
        ProgressPercent = Math.Max(1, current.ProgressPercent),
        Stage = "running",
        Detail = "The Agent is running the server-owned downloader operation."
    });

    public bool Progress(
        string operationId,
        int progressPercent,
        string stage,
        string detail,
        DownloaderOperationOutcomeDto? downloaderOutcome = null) =>
        Update(operationId, current => current with
        {
            State = DownloaderOperationStateDto.Running,
            Outcome = DownloaderOperationStateDto.Accepted,
            ProgressPercent = Math.Clamp(progressPercent, 0, 100),
            Stage = SafeStage(stage),
            Detail = SafeDetail(detail),
            DownloaderOutcome = downloaderOutcome ?? current.DownloaderOutcome
        }, ignoreFinal: true);

    public bool Complete(
        string operationId,
        DownloaderOperationStateDto outcome,
        string code,
        string detail,
        DownloaderOperationOutcomeDto downloaderOutcome)
    {
        var finalState = outcome;
        return Update(operationId, current => current with
        {
            State = finalState,
            Outcome = outcome,
            ProgressPercent = outcome == DownloaderOperationStateDto.Completed ? 100 : current.ProgressPercent,
            Stage = finalState switch
            {
                DownloaderOperationStateDto.Completed => "completed",
                DownloaderOperationStateDto.Failed => "failed",
                DownloaderOperationStateDto.OutcomeUnknown => "outcome-unknown",
                _ => "not-attempted"
            },
            Detail = SafeDetail(detail),
            CompletedAtUtc = clock.GetUtcNow(),
            DownloaderOutcome = downloaderOutcome,
            ErrorCode = SafeCode(code)
        }, completeSubscribers: true);
    }

    public async IAsyncEnumerable<DownloaderOperationDto> StreamAsync(
        string principalSid,
        string operationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Entry? entry;
        Channel<DownloaderOperationDto>? channel = null;
        DownloaderOperationDto? initial = null;
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (entries.TryGetValue(operationId, out entry)
                && string.Equals(entry.PrincipalSid, principalSid, StringComparison.Ordinal))
            {
                initial = entry.Current;
                if (!IsFinal(initial))
                {
                    channel = Channel.CreateBounded<DownloaderOperationDto>(new BoundedChannelOptions(32)
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
        Func<DownloaderOperationDto, DownloaderOperationDto> update,
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

    private static bool IsFinal(DownloaderOperationDto operation) =>
        operation.State is DownloaderOperationStateDto.NotAttempted
            or DownloaderOperationStateDto.Completed
            or DownloaderOperationStateDto.Failed
            or DownloaderOperationStateDto.OutcomeUnknown;

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
            ? "The Agent updated the downloader operation state."
            : value;

    private static string SafeCode(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 96
            || value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))
            ? "downloader_operation_completed"
            : value;

    private sealed class Entry(string principalSid, DownloaderOperationDto current)
    {
        public string PrincipalSid { get; } = principalSid;
        public DownloaderOperationDto Current { get; set; } = current;
        public List<Channel<DownloaderOperationDto>> Subscribers { get; } = [];
    }
}

public sealed record DownloaderOperationHandle(
    string OperationId,
    DownloaderOperationDto InitialState);

public sealed class DownloaderOperationCapacityException()
    : InvalidOperationException("The Agent downloader operation retention limit has been reached.");

public sealed class AgentOperationConcurrencyGate
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public AgentOperationLease? TryEnter() => gate.Wait(0) ? new(gate) : null;
}

public sealed class AgentOperationLease(SemaphoreSlim gate) : IDisposable
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
