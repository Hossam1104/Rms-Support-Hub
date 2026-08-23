namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public sealed record LogEvidenceResult(
    LogEvidenceViewState State,
    IReadOnlyList<LogEvidenceServiceRow> Services,
    DateTimeOffset? CheckedAtUtc,
    string? CorrelationId,
    string ErrorCode,
    string ErrorDetail)
{
    public static LogEvidenceResult Success(
        LogEvidenceViewState state,
        IReadOnlyList<LogEvidenceServiceRow> services,
        DateTimeOffset checkedAtUtc,
        string correlationId) => new(
        state,
        services,
        checkedAtUtc,
        correlationId,
        string.Empty,
        string.Empty);

    public static LogEvidenceResult Failure(
        LogEvidenceViewState state,
        string errorCode,
        string errorDetail,
        string? correlationId = null) => new(
        state,
        [],
        null,
        correlationId,
        errorCode,
        errorDetail);
}
