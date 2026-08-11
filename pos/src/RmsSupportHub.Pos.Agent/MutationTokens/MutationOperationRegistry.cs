namespace RmsSupportHub.Pos.Agent.MutationTokens;

/// <summary>
/// Server-owned description of a future typed mutation. It intentionally has no client-supplied
/// path, URL, command, service, SQL, or filesystem target.
/// </summary>
public sealed record MutationOperationDescriptor
{
    public MutationOperationDescriptor(string operationId, string httpMethod)
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

        OperationId = operationId;
        HttpMethod = httpMethod.ToUpperInvariant();
    }

    public string OperationId { get; }

    public string HttpMethod { get; }
}

public interface IMutationOperationRegistry
{
    bool TryGet(string? operationId, out MutationOperationDescriptor descriptor);
}

/// <summary>
/// Immutable process-local registry. Production INT-05 intentionally registers no feature
/// operations; later feature sessions add descriptors at the Agent composition root.
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
