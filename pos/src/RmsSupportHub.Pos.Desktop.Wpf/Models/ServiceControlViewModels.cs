using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Services;

namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public enum ServiceControlViewState
{
    Idle,
    Starting,
    Stopping,
    Restarting,
    Completed,
    Failed,
    OutcomeUnknown,
    Unauthorized,
    Unavailable,
    TimedOut,
    InvalidResponse,
    Cancelled
}

public sealed record ServiceControlAuthorizationResult(
    bool IsAdministrator,
    bool CanManageRmsServices,
    string ErrorCode,
    string ErrorDetail)
{
    public static ServiceControlAuthorizationResult Unavailable(string code, string detail) =>
        new(false, false, code, detail);
}

public sealed record ServiceActionResult(
    ServiceControlViewState State,
    string OperationId,
    string ServiceId,
    ServiceActionKind Action,
    int ProgressPercent,
    string Stage,
    ServiceRuntimeState? ObservedState,
    string Code,
    string Detail,
    string CorrelationId,
    bool RecoveryRequired)
{
    public static ServiceActionResult Failure(
        ServiceControlViewState state,
        string code,
        string detail,
        string serviceId,
        ServiceActionKind action,
        string correlationId) => new(
            state,
            string.Empty,
            serviceId,
            action,
            0,
            "unavailable",
            null,
            code,
            detail,
            correlationId,
            false);

    public string StateLabel => State switch
    {
        ServiceControlViewState.Starting => "Starting…",
        ServiceControlViewState.Stopping => "Stopping…",
        ServiceControlViewState.Restarting => "Restarting…",
        ServiceControlViewState.Completed => "Completed",
        ServiceControlViewState.OutcomeUnknown => "Outcome unknown",
        ServiceControlViewState.Unauthorized => "Administrator access required",
        ServiceControlViewState.TimedOut => "Timed out",
        ServiceControlViewState.Cancelled => "Cancelled",
        ServiceControlViewState.Unavailable => "Unavailable",
        ServiceControlViewState.InvalidResponse => "Invalid response",
        ServiceControlViewState.Failed => "Not completed",
        _ => "Ready"
    };
}
