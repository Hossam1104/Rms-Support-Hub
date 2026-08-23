using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;

internal static class DatabaseHealthTestSupport
{
    public static DatabaseHealthQueryHandler CreateHandler(
        RmsDatabaseDiagnosticStatus branchStatus = RmsDatabaseDiagnosticStatus.Reachable,
        RmsDatabaseDiagnosticStatus cashierStatus = RmsDatabaseDiagnosticStatus.Reachable,
        TimeSpan? timeout = null,
        Exception? exception = null,
        TimeSpan? delay = null) =>
        new(
            new FixedDatabaseDiagnostics(branchStatus, cashierStatus, exception, delay),
            TimeProvider.System,
            timeout);

    private sealed class FixedDatabaseDiagnostics(
        RmsDatabaseDiagnosticStatus branchStatus,
        RmsDatabaseDiagnosticStatus cashierStatus,
        Exception? exception,
        TimeSpan? delay) : IRmsDatabaseDiagnostics
    {
        public async Task<RmsDatabaseDiagnosticResult> DiagnoseAsync(
            RmsDatabaseKind database,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (delay is { } wait)
            {
                await Task.Delay(wait, cancellationToken);
            }

            if (exception is not null)
            {
                throw exception;
            }

            var expected = RmsDatabaseCatalog.For(database).DatabaseName;
            var status = database == RmsDatabaseKind.Branch ? branchStatus : cashierStatus;
            var configured = status is not RmsDatabaseDiagnosticStatus.NotConfigured
                and not RmsDatabaseDiagnosticStatus.ConfigurationInvalid;
            var configuredDatabase = configured
                ? status == RmsDatabaseDiagnosticStatus.DatabaseNameMismatch ? "OtherDatabase" : expected
                : null;
            var serverDisplay = configured ? "integration-sql:1433" : null;
            bool? nameMatches = status switch
            {
                RmsDatabaseDiagnosticStatus.Reachable => true,
                RmsDatabaseDiagnosticStatus.DatabaseNameMismatch => false,
                _ => null
            };

            return new RmsDatabaseDiagnosticResult(
                database,
                expected,
                configuredDatabase,
                serverDisplay,
                configured,
                nameMatches,
                status,
                DateTimeOffset.UtcNow,
                "safe test detail");
        }

        public Task<RmsDatabaseDiagnosticResult> DiagnoseServerAsync(
            RmsDatabaseKind database,
            CancellationToken cancellationToken = default) =>
            DiagnoseAsync(database, cancellationToken);
    }
}
