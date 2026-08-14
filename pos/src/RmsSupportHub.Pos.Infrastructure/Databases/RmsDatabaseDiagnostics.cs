using Microsoft.Data.SqlClient;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Databases;

/// <summary>
/// Performs the bounded, read-only database diagnostic against the connection string owned by the
/// installed RMS component. The raw string exists only for the duration of the probe call.
/// </summary>
public sealed class RmsDatabaseDiagnostics(
    IRmsInstallationDiscovery discovery,
    IRmsDatabaseConnectionStringSource connectionStrings,
    IRmsSqlReadOnlyProbe sqlProbe,
    TimeProvider clock) : IRmsDatabaseDiagnostics
{
    public async Task<RmsDatabaseDiagnosticResult> DiagnoseAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken = default)
    {
        var expected = database switch
        {
            RmsDatabaseKind.Branch => "RmsBranchSrv",
            RmsDatabaseKind.Cashier => "RmsCashierSrv",
            _ => throw new ArgumentOutOfRangeException(nameof(database), database, "Unknown RMS database.")
        };
        var snapshot = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        var configuration = database == RmsDatabaseKind.Branch
            ? snapshot.BranchDatabase
            : snapshot.CashierDatabase;
        var checkedAt = clock.GetUtcNow();
        var raw = await connectionStrings.GetConnectionStringAsync(database, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(raw))
        {
            var configurationStatus = configuration.ConnectionStringState == RmsConnectionStringState.Invalid
                ? RmsDatabaseDiagnosticStatus.ConfigurationInvalid
                : RmsDatabaseDiagnosticStatus.NotConfigured;
            return new(
                database,
                expected,
                configuration.DatabaseName,
                configuration.DataSource,
                configurationStatus == RmsDatabaseDiagnosticStatus.ConfigurationInvalid,
                null,
                configurationStatus,
                checkedAt,
                configurationStatus == RmsDatabaseDiagnosticStatus.ConfigurationInvalid
                    ? "The installed RMS database configuration file is invalid."
                    : "The installed RMS database connection is not configured.");
        }

        if (configuration.ConnectionStringState != RmsConnectionStringState.Valid
            || string.IsNullOrWhiteSpace(configuration.DatabaseName)
            || string.IsNullOrWhiteSpace(configuration.DataSource))
        {
            return new(
                database,
                expected,
                configuration.DatabaseName,
                configuration.DataSource,
                true,
                configuration.DatabaseName is not null
                    ? string.Equals(configuration.DatabaseName, expected, StringComparison.OrdinalIgnoreCase)
                    : null,
                RmsDatabaseDiagnosticStatus.ConfigurationInvalid,
                checkedAt,
                "The installed RMS database connection configuration is invalid.");
        }

        var databaseNameMatches = string.Equals(
            configuration.DatabaseName,
            expected,
            StringComparison.OrdinalIgnoreCase);
        if (!databaseNameMatches)
        {
            // Do not open an unexpected database merely to report a diagnostic. The configured
            // name remains visible as sanitized evidence, while future mutating workflows must
            // refuse this mismatch as well.
            return new(
                database,
                expected,
                configuration.DatabaseName,
                configuration.DataSource,
                true,
                false,
                RmsDatabaseDiagnosticStatus.DatabaseNameMismatch,
                checkedAt,
                $"The installed RMS configuration names a database other than {expected}.");
        }

        var probe = await sqlProbe.ProbeAsync(raw, cancellationToken).ConfigureAwait(false);
        var status = probe.Status switch
        {
            RmsSqlProbeStatus.Reachable when string.Equals(
                probe.DatabaseName,
                expected,
                StringComparison.OrdinalIgnoreCase) => RmsDatabaseDiagnosticStatus.Reachable,
            RmsSqlProbeStatus.Reachable => RmsDatabaseDiagnosticStatus.DatabaseNameMismatch,
            RmsSqlProbeStatus.AuthenticationFailed => RmsDatabaseDiagnosticStatus.AuthenticationFailed,
            RmsSqlProbeStatus.DatabaseUnavailable => RmsDatabaseDiagnosticStatus.DatabaseUnavailable,
            _ => RmsDatabaseDiagnosticStatus.Unreachable
        };
        var detail = status switch
        {
            RmsDatabaseDiagnosticStatus.Reachable => "The configured RMS database answered the read-only identity probe.",
            RmsDatabaseDiagnosticStatus.DatabaseNameMismatch => "The SQL connection did not resolve to the expected RMS database.",
            RmsDatabaseDiagnosticStatus.AuthenticationFailed => "SQL authentication failed for the installed RMS database configuration.",
            RmsDatabaseDiagnosticStatus.DatabaseUnavailable => "The configured SQL server was reachable but the RMS database was unavailable.",
            _ => "The configured RMS database could not be reached."
        };

        return new(
            database,
            expected,
            configuration.DatabaseName,
            configuration.DataSource,
            true,
            status == RmsDatabaseDiagnosticStatus.Reachable
                ? true
                : status == RmsDatabaseDiagnosticStatus.DatabaseNameMismatch ? false : databaseNameMatches,
            status,
            checkedAt,
            detail);
    }
}

/// <summary>SQL client adapter for the single fixed SELECT DB_NAME() diagnostic.</summary>
public sealed class SqlClientReadOnlyProbe : IRmsSqlReadOnlyProbe
{
    public async Task<RmsSqlProbeResult> ProbeAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            builder.ConnectTimeout = Math.Clamp(builder.ConnectTimeout, 1, 5);

            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT DB_NAME()";
            command.CommandTimeout = 5;
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var databaseName = value as string;
            return string.IsNullOrWhiteSpace(databaseName)
                ? new(RmsSqlProbeStatus.DatabaseUnavailable, null)
                : new(RmsSqlProbeStatus.Reachable, databaseName.Trim());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SqlException exception)
        {
            return new(Classify(exception.Number), null);
        }
        catch
        {
            return new(RmsSqlProbeStatus.Unreachable, null);
        }
    }

    private static RmsSqlProbeStatus Classify(int number) => number switch
    {
        18452 or 18456 or 18468 or 18470 or 18487 or 18488 => RmsSqlProbeStatus.AuthenticationFailed,
        4060 => RmsSqlProbeStatus.DatabaseUnavailable,
        -2 or 26 or 40 or 53 or 10060 or 10061 => RmsSqlProbeStatus.Unreachable,
        _ => RmsSqlProbeStatus.DatabaseUnavailable
    };
}
