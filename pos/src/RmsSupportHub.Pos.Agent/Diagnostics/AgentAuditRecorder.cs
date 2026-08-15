using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Diagnostics;

/// <summary>Writes safe operation outcomes through the durable Agent audit boundary.</summary>
public static class AgentAuditRecorder
{
    public static void Record(
        IAgentAuditSink sink,
        string principal,
        string operation,
        string? target,
        string correlationId,
        string outcome,
        string? failureCode = null) =>
        sink.Record(new AgentAuditEvent(
            DateTimeOffset.UtcNow,
            principal,
            operation,
            target,
            correlationId,
            outcome,
            failureCode,
            typeof(AgentAuditRecorder).Assembly.GetName().Version?.ToString(3) ?? "unavailable",
            null));
}
