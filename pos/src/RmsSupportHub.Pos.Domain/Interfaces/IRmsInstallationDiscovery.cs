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
