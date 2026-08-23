using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

public sealed class DatabaseHealthQueryTests
{
    [Fact]
    public async Task BothCanonicalDatabasesReachableProduceHealthySnapshot()
    {
        var diagnostics = new FakeDatabaseDiagnostics(RmsDatabaseDiagnosticStatus.Reachable);
        var result = await CreateHandler(diagnostics).HandleAsync(LocalOperatorContext("database-healthy"));

        Assert.True(result.Succeeded);
        var snapshot = Assert.IsType<DatabaseHealthSnapshot>(result.Value);
        Assert.Equal(DatabaseHealthOverallState.Healthy, snapshot.OverallState);
        Assert.Equal(
            new[] { RmsDatabaseKind.Branch, RmsDatabaseKind.Cashier },
            snapshot.Databases.Select(item => item.DatabaseKind));
        Assert.All(snapshot.Databases, item =>
        {
            Assert.Equal(RmsDatabaseDiagnosticStatus.Reachable, item.Status);
            Assert.Equal("Connected", item.SafeDetail);
            Assert.True(item.DatabaseNameMatches);
        });
        Assert.Equal(2, diagnostics.CallCount);
    }

    [Theory]
    [InlineData(RmsDatabaseDiagnosticStatus.AuthenticationFailed)]
    [InlineData(RmsDatabaseDiagnosticStatus.DatabaseUnavailable)]
    [InlineData(RmsDatabaseDiagnosticStatus.Unreachable)]
    public async Task OneUnavailableDatabaseProducesDegradedSnapshot(
        RmsDatabaseDiagnosticStatus unavailableStatus)
    {
        var diagnostics = new FakeDatabaseDiagnostics(
            RmsDatabaseDiagnosticStatus.Reachable,
            unavailableStatus);

        var result = await CreateHandler(diagnostics).HandleAsync(LocalOperatorContext("database-degraded"));

        Assert.True(result.Succeeded);
        Assert.Equal(DatabaseHealthOverallState.Degraded, result.Value!.OverallState);
        Assert.Equal("Connected", result.Value.Databases[0].SafeDetail);
        Assert.Equal(unavailableStatus, result.Value.Databases[1].Status);
    }

    [Fact]
    public async Task ConfigurationProblemsAreProjectedAsDegradedWithoutNativeDetails()
    {
        var diagnostics = new FakeDatabaseDiagnostics(
            RmsDatabaseDiagnosticStatus.DatabaseNameMismatch,
            RmsDatabaseDiagnosticStatus.NotConfigured);

        var result = await CreateHandler(diagnostics).HandleAsync(LocalOperatorContext("database-config"));

        Assert.True(result.Succeeded);
        Assert.Equal(DatabaseHealthOverallState.Degraded, result.Value!.OverallState);
        Assert.Equal("Database mismatch", result.Value.Databases[0].SafeDetail);
        Assert.Equal("Not configured", result.Value.Databases[1].SafeDetail);
        Assert.All(result.Value.Databases, item =>
        {
            Assert.DoesNotContain("password", item.SafeDetail, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("exception", item.SafeDetail, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task BothConfigurationInvalidDatabasesProduceDeterministicDegradedSnapshot()
    {
        var diagnostics = new FakeDatabaseDiagnostics(
            RmsDatabaseDiagnosticStatus.ConfigurationInvalid,
            RmsDatabaseDiagnosticStatus.ConfigurationInvalid);

        var result = await CreateHandler(diagnostics).HandleAsync(LocalOperatorContext("database-invalid-config"));

        Assert.True(result.Succeeded);
        Assert.Equal(DatabaseHealthOverallState.Degraded, result.Value!.OverallState);
        Assert.All(result.Value.Databases, item =>
            Assert.Equal("Configuration invalid", item.SafeDetail));
    }

    [Fact]
    public async Task BothUnavailableDatabasesProduceUnavailableSnapshot()
    {
        var diagnostics = new FakeDatabaseDiagnostics(
            RmsDatabaseDiagnosticStatus.AuthenticationFailed,
            RmsDatabaseDiagnosticStatus.Unreachable);

        var result = await CreateHandler(diagnostics).HandleAsync(LocalOperatorContext("database-unavailable"));

        Assert.True(result.Succeeded);
        Assert.Equal(DatabaseHealthOverallState.Unavailable, result.Value!.OverallState);
    }

    [Fact]
    public async Task UnexpectedLookupFailureReturnsFixedSafeError()
    {
        var diagnostics = new FakeDatabaseDiagnostics(
            RmsDatabaseDiagnosticStatus.Reachable,
            RmsDatabaseDiagnosticStatus.Reachable,
            new InvalidOperationException("password=secret; native stack trace"));

        var result = await CreateHandler(diagnostics).HandleAsync(LocalOperatorContext("database-error"));

        Assert.False(result.Succeeded);
        Assert.Equal("database_health_unavailable", result.Error?.Code);
        Assert.Equal("RMS database health is currently unavailable.", result.Error?.Message);
        Assert.DoesNotContain("secret", result.Error?.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", result.Error?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BoundedLookupTimeoutReturnsFixedSafeError()
    {
        var diagnostics = new FakeDatabaseDiagnostics(
            RmsDatabaseDiagnosticStatus.Reachable,
            RmsDatabaseDiagnosticStatus.Reachable,
            delay: Timeout.InfiniteTimeSpan);
        var handler = CreateHandler(diagnostics, TimeSpan.FromMilliseconds(20));

        var result = await handler.HandleAsync(LocalOperatorContext("database-timeout"));

        Assert.False(result.Succeeded);
        Assert.Equal("database_health_timeout", result.Error?.Code);
        Assert.Equal("Database health check timed out.", result.Error?.Message);
    }

    [Fact]
    public async Task ExternalCancellationIsPreserved()
    {
        var diagnostics = new FakeDatabaseDiagnostics(
            RmsDatabaseDiagnosticStatus.Reachable,
            RmsDatabaseDiagnosticStatus.Reachable,
            delay: Timeout.InfiniteTimeSpan);
        var handler = CreateHandler(diagnostics, TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync(LocalOperatorContext("database-cancelled"), cancellation.Token));
    }

    [Fact]
    public async Task MissingUnauthenticatedRemoteAndInvalidContextsAreDeniedBeforeLookup()
    {
        var diagnostics = new FakeDatabaseDiagnostics(RmsDatabaseDiagnosticStatus.Reachable);
        var handler = CreateHandler(diagnostics);

        var missing = await handler.HandleAsync(null);
        var unauthenticated = await handler.HandleAsync(new InvocationContext(
            InvocationSource.LocalWpf,
            "unauthenticated",
            InvocationAuthorizationLevel.Unauthenticated,
            "database-unauthenticated"));
        var remote = await handler.HandleAsync(new InvocationContext(
            InvocationSource.RemoteHub,
            "remote-admin",
            InvocationAuthorizationLevel.RemoteAdministrator,
            "database-remote"));
        var invalid = await handler.HandleAsync(new InvocationContext(
            (InvocationSource)999,
            "invalid-source",
            InvocationAuthorizationLevel.LocalAdministrator,
            "database-invalid"));

        Assert.Equal("invocation_context_missing", missing.Error?.Code);
        Assert.Equal("diagnostic_authorization_required", unauthenticated.Error?.Code);
        Assert.Equal("diagnostic_authorization_required", remote.Error?.Code);
        Assert.Equal("diagnostic_authorization_required", invalid.Error?.Code);
        Assert.Equal(0, diagnostics.CallCount);
    }

    [Fact]
    public async Task LocalAdministratorCanUseTheSameReadOnlyProjection()
    {
        var result = await CreateHandler(new FakeDatabaseDiagnostics(RmsDatabaseDiagnosticStatus.Reachable))
            .HandleAsync(new InvocationContext(
                InvocationSource.LocalWpf,
                "local-admin",
                InvocationAuthorizationLevel.LocalAdministrator,
                "database-admin"));

        Assert.True(result.Succeeded);
        Assert.Equal(DatabaseHealthOverallState.Healthy, result.Value!.OverallState);
    }

    private static DatabaseHealthQueryHandler CreateHandler(
        FakeDatabaseDiagnostics diagnostics,
        TimeSpan? timeout = null) =>
        new(diagnostics, TimeProvider.System, timeout);

    private static InvocationContext LocalOperatorContext(string correlationId) => new(
        InvocationSource.LocalWpf,
        "local-operator",
        InvocationAuthorizationLevel.LocalOperator,
        correlationId);

    private sealed class FakeDatabaseDiagnostics(
        RmsDatabaseDiagnosticStatus branchStatus,
        RmsDatabaseDiagnosticStatus? cashierStatus = null,
        Exception? exception = null,
        TimeSpan? delay = null) : IRmsDatabaseDiagnostics
    {
        private readonly RmsDatabaseDiagnosticStatus cashierStatus =
            cashierStatus ?? branchStatus;

        public int CallCount { get; private set; }

        public async Task<RmsDatabaseDiagnosticResult> DiagnoseAsync(
            RmsDatabaseKind database,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (delay is { } wait)
            {
                await Task.Delay(wait, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (exception is not null)
            {
                throw exception;
            }

            var expected = RmsDatabaseCatalog.For(database).DatabaseName;
            var status = database == RmsDatabaseKind.Branch ? branchStatus : cashierStatus;
            var configured = status is not RmsDatabaseDiagnosticStatus.NotConfigured
                and not RmsDatabaseDiagnosticStatus.ConfigurationInvalid;
            return new(
                database,
                expected,
                configured ? status == RmsDatabaseDiagnosticStatus.DatabaseNameMismatch ? "OtherDatabase" : expected : null,
                configured ? "integration-sql:1433" : null,
                configured,
                status switch
                {
                    RmsDatabaseDiagnosticStatus.Reachable => true,
                    RmsDatabaseDiagnosticStatus.DatabaseNameMismatch => false,
                    _ => null
                },
                status,
                DateTimeOffset.UtcNow,
                "native exception details are intentionally not projected");
        }

        public Task<RmsDatabaseDiagnosticResult> DiagnoseServerAsync(
            RmsDatabaseKind database,
            CancellationToken cancellationToken = default) =>
            DiagnoseAsync(database, cancellationToken);
    }
}
