namespace RmsSupportHub.Pos.Agent.MutationTokens;

public enum MutationTargetKind
{
    None,
    AllowListedService,
    AllowListedRmsDatabase
}

/// <summary>
/// Server-owned description of a typed mutation. It intentionally has no client-supplied
/// path, URL, command, service, SQL, or filesystem target.
/// </summary>
public sealed record MutationOperationDescriptor
{
    public MutationOperationDescriptor(
        string operationId,
        string httpMethod,
        string? httpPathTemplate = null,
        MutationTargetKind targetKind = MutationTargetKind.None)
    {
        if (string.IsNullOrWhiteSpace(operationId)
            || operationId.Length > 128
            || operationId.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            throw new ArgumentException("A stable operation identifier is required.", nameof(operationId));
        }

        if (string.IsNullOrWhiteSpace(httpMethod) || httpMethod.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("A target HTTP method is required.", nameof(httpMethod));
        }

        if (httpPathTemplate is not null
            && (string.IsNullOrWhiteSpace(httpPathTemplate)
                || httpPathTemplate.Length > 512
                || !httpPathTemplate.StartsWith("/", StringComparison.Ordinal)
                || httpPathTemplate.Contains("?", StringComparison.Ordinal)
                || httpPathTemplate.Contains("#", StringComparison.Ordinal)
                || httpPathTemplate.Any(char.IsControl)
                || httpPathTemplate.Any(char.IsWhiteSpace)))
        {
            throw new ArgumentException("A canonical target HTTP path template is required.", nameof(httpPathTemplate));
        }

        if (targetKind != MutationTargetKind.None
            && (httpPathTemplate is null || !httpPathTemplate.Contains("{targetId}", StringComparison.Ordinal)))
        {
            throw new ArgumentException("A target-bound operation must expose a targetId path placeholder.", nameof(httpPathTemplate));
        }

        if (targetKind == MutationTargetKind.None
            && httpPathTemplate is not null
            && httpPathTemplate.Contains("{targetId}", StringComparison.Ordinal))
        {
            throw new ArgumentException("A targetId path placeholder requires a target-bound operation.", nameof(targetKind));
        }

        OperationId = operationId;
        HttpMethod = httpMethod.ToUpperInvariant();
        HttpPathTemplate = httpPathTemplate;
        TargetKind = targetKind;
    }

    public string OperationId { get; }

    public string HttpMethod { get; }

    public string? HttpPathTemplate { get; }

    public MutationTargetKind TargetKind { get; }

    public bool TryResolveHttpPath(string? targetId, out string? httpPath)
    {
        httpPath = null;

        if (TargetKind == MutationTargetKind.None)
        {
            if (!string.IsNullOrEmpty(targetId))
            {
                return false;
            }

            httpPath = HttpPathTemplate;
            return true;
        }

        if (string.IsNullOrWhiteSpace(targetId)
            || targetId.Length > 128
            || targetId.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))
            || targetId.Contains("/", StringComparison.Ordinal)
            || targetId.Contains("\\", StringComparison.Ordinal)
            || targetId.Contains("?", StringComparison.Ordinal)
            || targetId.Contains("#", StringComparison.Ordinal))
        {
            return false;
        }

        httpPath = HttpPathTemplate!.Replace(
            "{targetId}",
            Uri.EscapeDataString(targetId),
            StringComparison.Ordinal);
        return true;
    }
}

public interface IMutationOperationRegistry
{
    bool TryGet(string? operationId, out MutationOperationDescriptor descriptor);
}

/// <summary>
/// Immutable process-local registry. Each Agent feature registers only its explicit typed
/// descriptors at the composition root; the current production composition registers the single
/// INT-08 service-control operation.
/// </summary>
public sealed class MutationOperationRegistry : IMutationOperationRegistry
{
    private readonly IReadOnlyDictionary<string, MutationOperationDescriptor> _operations;

    public MutationOperationRegistry(IEnumerable<MutationOperationDescriptor>? operations = null)
    {
        var descriptors = operations ?? [];
        var map = new Dictionary<string, MutationOperationDescriptor>(StringComparer.Ordinal);
        foreach (var operation in descriptors)
        {
            ArgumentNullException.ThrowIfNull(operation);
            if (!map.TryAdd(operation.OperationId, operation))
            {
                throw new InvalidOperationException($"Duplicate mutation operation ID: {operation.OperationId}.");
            }
        }

        _operations = map;
    }

    public bool TryGet(string? operationId, out MutationOperationDescriptor descriptor)
    {
        if (!string.IsNullOrWhiteSpace(operationId)
            && _operations.TryGetValue(operationId, out var found))
        {
            descriptor = found;
            return true;
        }

        descriptor = null!;
        return false;
    }
}
