namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>
/// Safe durable audit record. Target is always an opaque server-owned identifier; secrets,
/// credentials, paths, package URLs, and private certificate material are not valid fields.
/// </summary>
public sealed record AgentAuditEvent(
    DateTimeOffset AtUtc,
    string Principal,
    string Operation,
    string? Target,
    string CorrelationId,
    string Outcome,
    string? FailureCode,
    string ProductVersion,
    string? BuildId)
{
    public string? Source { get; init; }

    public string? PackageId { get; init; }

    public string? PackageVersion { get; init; }

    public string? TrustResult { get; init; }

    public string? RecoveryState { get; init; }
}

public interface IAgentAuditSink
{
    void Record(AgentAuditEvent auditEvent);
}

public interface IAgentAuditReader
{
    Task<IReadOnlyList<AgentAuditEvent>> ReadRecentAsync(
        int maximumEntries,
        CancellationToken cancellationToken = default);
}
