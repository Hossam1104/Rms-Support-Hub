using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Interfaces;

/// <summary>Privileged, non-transport audit sink for typed RMS database operations.</summary>
public interface IRmsPrivilegedAuditSink
{
    /// <summary>
    /// Records one privileged database event and reports whether it reached durable storage.
    /// Required pre-mutation events must fail closed when this returns false.
    /// </summary>
    bool Record(RmsPrivilegedAuditEvent auditEvent);
}
