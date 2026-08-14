using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Interfaces;

/// <summary>Privileged, non-transport audit sink for typed RMS database operations.</summary>
public interface IRmsPrivilegedAuditSink
{
    void Record(RmsPrivilegedAuditEvent auditEvent);
}
