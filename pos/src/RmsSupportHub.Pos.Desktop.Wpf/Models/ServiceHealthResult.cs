namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public sealed record ServiceHealthResult(
    ServiceHealthViewState State,
    IReadOnlyList<ServiceHealthRow> Services,
    DateTimeOffset? CheckedAtUtc,
    string? CorrelationId,
    string ErrorCode,
    string ErrorDetail)
{
    public static ServiceHealthResult Healthy(
        ServiceHealthViewState state,
        IReadOnlyList<ServiceHealthRow> services,
        DateTimeOffset checkedAtUtc,
        string correlationId) => new(
        state,
        services,
        checkedAtUtc,
        correlationId,
        string.Empty,
        string.Empty);

    public static ServiceHealthResult Failure(
        ServiceHealthViewState state,
        string errorCode,
        string errorDetail,
        string? correlationId = null) => new(
        state,
        [],
        null,
        correlationId,
        errorCode,
        errorDetail);

    public int RunningCount => Services.Count(service => service.State == ServiceHealthRowState.Running);

    public int StoppedCount => Services.Count(service => service.State == ServiceHealthRowState.Stopped);

    public int UnknownCount => Services.Count(service => service.State is
        ServiceHealthRowState.Unknown
        or ServiceHealthRowState.Paused
        or ServiceHealthRowState.Transitioning);

    public int NotInstalledCount => Services.Count(service => service.State == ServiceHealthRowState.NotInstalled);
}
