using RmsSupportHub.Pos.Agent.Services;

namespace RmsSupportHub.Pos.Agent.MutationTokens;

/// <summary>
/// Resolves target-bound mutation identifiers through typed Agent-owned allow-lists. This keeps the
/// generic token endpoint from accepting a browser-provided service name or arbitrary path.
/// </summary>
public interface IMutationOperationTargetResolver
{
    Task<bool> IsAllowedAsync(
        MutationOperationDescriptor operation,
        string? targetId,
        CancellationToken cancellationToken = default);
}

public sealed class MutationOperationTargetResolver(ServiceAllowList services)
    : IMutationOperationTargetResolver
{
    public async Task<bool> IsAllowedAsync(
        MutationOperationDescriptor operation,
        string? targetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return operation.TargetKind switch
        {
            MutationTargetKind.None => string.IsNullOrEmpty(targetId),
            MutationTargetKind.AllowListedService =>
                await services.ResolveAsync(targetId, cancellationToken).ConfigureAwait(false) is not null,
            _ => false
        };
    }
}
