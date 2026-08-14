using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Interfaces;

/// <summary>Reads the known installed RMS+ files and returns a sanitized internal snapshot.</summary>
public interface IRmsInstallationDiscovery
{
    Task<RmsInstallationSnapshot> DiscoverAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Dedicated secret-bearing seam used only by database diagnostics. Implementations must read the
/// selected connection string directly from the installed RMS configuration and must not persist,
/// log, or expose it through a general discovery model.
/// </summary>
public interface IRmsDatabaseConnectionStringSource
{
    Task<string?> GetConnectionStringAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken = default);
}

/// <summary>Executes the one fixed read-only SQL probe used by RMS database diagnostics.</summary>
public interface IRmsSqlReadOnlyProbe
{
    Task<RmsSqlProbeResult> ProbeAsync(
        string connectionString,
        CancellationToken cancellationToken = default);
}

/// <summary>Runs sanitized diagnostics for the installed Branch or Cashier database.</summary>
public interface IRmsDatabaseDiagnostics
{
    Task<RmsDatabaseDiagnosticResult> DiagnoseAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Diagnoses only whether the installed configuration names the canonical database and whether
    /// the SQL Server hosting it can be reached with the installed credentials via the fixed
    /// <c>master</c> catalog. Unlike <see cref="DiagnoseAsync"/>, this never requires the target
    /// database itself to open, so it stays usable when the target database is missing, offline, or
    /// damaged -- the exact conditions a database restore must be able to recover from.
    /// </summary>
    Task<RmsDatabaseDiagnosticResult> DiagnoseServerAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken = default);
}

public sealed record RmsDatabaseDiagnosticResult(
    RmsDatabaseKind Database,
    string ExpectedDatabase,
    string? ConfiguredDatabase,
    string? ServerDisplay,
    bool Configured,
    bool? DatabaseNameMatches,
    RmsDatabaseDiagnosticStatus Status,
    DateTimeOffset CheckedAtUtc,
    string Detail);

public enum RmsDatabaseDiagnosticStatus
{
    NotConfigured,
    ConfigurationInvalid,
    DatabaseNameMismatch,
    Reachable,
    AuthenticationFailed,
    DatabaseUnavailable,
    Unreachable
}
