using RmsSupportHub.Pos.Domain.Enums;

namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>Bounded, transport-neutral state exposed while a typed service action executes.</summary>
public enum ServiceControlOperationState
{
    Queued,
    Accepted,
    Running,
    Completed,
    Failed,
    OutcomeUnknown,
    Cancelled
}

/// <summary>
/// Safe result for one server-owned RMS service action. It contains no native service name,
/// executable, process, exception, or command detail.
/// </summary>
public sealed record ServiceControlOperationResult(
    string OperationId,
    string ServiceId,
    ServiceControlAction Action,
    ServiceControlOperationState State,
    int ProgressPercent,
    string Stage,
    ServiceStatus? ObservedState,
    string Code,
    string Detail,
    string CorrelationId,
    bool RecoveryRequired = false);
