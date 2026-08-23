namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public sealed record DatabaseHealthResult(
    DatabaseHealthViewState State,
    IReadOnlyList<DatabaseHealthRow> Databases,
    DateTimeOffset? CheckedAtUtc,
    string? CorrelationId,
    string ErrorCode,
    string ErrorDetail)
{
    public static DatabaseHealthResult Success(
        DatabaseHealthViewState state,
        IReadOnlyList<DatabaseHealthRow> databases,
        DateTimeOffset checkedAtUtc,
        string correlationId) => new(
        state,
        databases,
        checkedAtUtc,
        correlationId,
        string.Empty,
        string.Empty);

    public static DatabaseHealthResult Failure(
        DatabaseHealthViewState state,
        string errorCode,
        string errorDetail,
        string? correlationId = null) => new(
        state,
        [],
        null,
        correlationId,
        errorCode,
        errorDetail);

    public int ConnectedCount => Databases.Count(database => database.IsConnected);

    public int AttentionCount => Databases.Count(database => !database.IsConnected);
}
