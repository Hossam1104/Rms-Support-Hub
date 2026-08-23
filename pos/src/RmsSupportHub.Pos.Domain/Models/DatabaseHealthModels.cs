using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Domain.Models;

public enum DatabaseHealthOverallState
{
    Healthy,
    Degraded,
    Unavailable,
    Unknown
}

/// <summary>
/// Sanitized health for one server-owned canonical RMS database. The raw connection string and
/// SQL probe detail never enter this read model.
/// </summary>
public sealed record DatabaseHealthItem(
    RmsDatabaseKind DatabaseKind,
    string DisplayName,
    string ExpectedDatabase,
    string? ConfiguredDatabase,
    string? ServerDisplay,
    bool Configured,
    bool? DatabaseNameMatches,
    RmsDatabaseDiagnosticStatus Status,
    string SafeDetail,
    DateTimeOffset CheckedAtUtc);

public sealed record DatabaseHealthSnapshot(
    DateTimeOffset CheckedAtUtc,
    DatabaseHealthOverallState OverallState,
    IReadOnlyList<DatabaseHealthItem> Databases);
