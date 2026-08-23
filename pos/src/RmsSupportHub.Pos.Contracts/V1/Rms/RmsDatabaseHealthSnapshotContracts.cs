namespace RmsSupportHub.Pos.Contracts.V1.Rms;

public enum RmsDatabaseHealthOverallState
{
    Healthy,
    Degraded,
    Unavailable,
    Unknown
}

/// <summary>Typed, fixed-set database health projection returned over Local IPC.</summary>
public sealed record RmsDatabaseHealthSnapshotDto(
    DateTimeOffset CheckedAtUtc,
    RmsDatabaseHealthOverallState OverallState,
    IReadOnlyList<RmsDatabaseHealthItemDto> Databases);

/// <summary>
/// One canonical Branch or Cashier row. This contract intentionally contains no credentials,
/// connection string, SQL text, or caller-selected database input.
/// </summary>
public sealed record RmsDatabaseHealthItemDto(
    RmsDatabaseTarget DatabaseKind,
    string DisplayName,
    string ExpectedDatabase,
    string? ConfiguredDatabase,
    string? ServerDisplay,
    bool Configured,
    bool? DatabaseNameMatches,
    RmsDatabaseDiagnosticStatus Status,
    string SafeDetail,
    DateTimeOffset CheckedAtUtc);
