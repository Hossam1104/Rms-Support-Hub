using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Databases;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

public sealed class RmsDatabaseDiagnosticsTests
{
    [Fact]
    public async Task DiagnoseServerAsync_ReachableMaster_ReturnsReachableRegardlessOfLiveDatabaseName()
    {
        // The master probe answers with "master" as the resolved database name, which must not be
        // compared against the canonical target: only master connectivity itself is being proven.
        var probe = new FakeProbe { Result = new(RmsSqlProbeStatus.Reachable, "master") };
        var diagnostics = CreateDiagnostics(ValidConfiguration("RmsBranchSrv"), "Server=.;Database=RmsBranchSrv;", probe);

        var result = await diagnostics.DiagnoseServerAsync(RmsDatabaseKind.Branch);

        Assert.Equal(RmsDatabaseDiagnosticStatus.Reachable, result.Status);
        Assert.True(result.DatabaseNameMatches);
        var usedBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(probe.LastConnectionString);
        Assert.Equal("master", usedBuilder.InitialCatalog);
    }

    [Fact]
    public async Task DiagnoseServerAsync_MissingTargetDatabase_StillReportsReachable()
    {
        // Simulates a target database that does not currently exist: the probe connects fine to
        // master, so Restore must remain eligible even though the target catalog is absent.
        var probe = new FakeProbe { Result = new(RmsSqlProbeStatus.Reachable, "master") };
        var diagnostics = CreateDiagnostics(ValidConfiguration("RmsCashierSrv"), "Server=.;Database=RmsCashierSrv;", probe);

        var result = await diagnostics.DiagnoseServerAsync(RmsDatabaseKind.Cashier);

        Assert.Equal(RmsDatabaseDiagnosticStatus.Reachable, result.Status);
    }

    [Fact]
    public async Task DiagnoseServerAsync_AuthenticationFailure_IsRejected()
    {
        var probe = new FakeProbe { Result = new(RmsSqlProbeStatus.AuthenticationFailed, null) };
        var diagnostics = CreateDiagnostics(ValidConfiguration("RmsBranchSrv"), "Server=.;Database=RmsBranchSrv;", probe);

        var result = await diagnostics.DiagnoseServerAsync(RmsDatabaseKind.Branch);

        Assert.Equal(RmsDatabaseDiagnosticStatus.AuthenticationFailed, result.Status);
    }

    [Theory]
    [InlineData(RmsSqlProbeStatus.Unreachable)]
    [InlineData(RmsSqlProbeStatus.DatabaseUnavailable)]
    public async Task DiagnoseServerAsync_ServerUnreachable_IsRejected(RmsSqlProbeStatus probeStatus)
    {
        var probe = new FakeProbe { Result = new(probeStatus, null) };
        var diagnostics = CreateDiagnostics(ValidConfiguration("RmsBranchSrv"), "Server=.;Database=RmsBranchSrv;", probe);

        var result = await diagnostics.DiagnoseServerAsync(RmsDatabaseKind.Branch);

        Assert.Equal(RmsDatabaseDiagnosticStatus.Unreachable, result.Status);
    }

    [Fact]
    public async Task DiagnoseServerAsync_ConfiguredDatabaseNameMismatch_RejectsWithoutAttemptingSqlConnection()
    {
        var probe = new FakeProbe { Result = new(RmsSqlProbeStatus.Reachable, "master") };
        var diagnostics = CreateDiagnostics(ValidConfiguration("SomeOtherDatabase"), "Server=.;Database=SomeOtherDatabase;", probe);

        var result = await diagnostics.DiagnoseServerAsync(RmsDatabaseKind.Branch);

        Assert.Equal(RmsDatabaseDiagnosticStatus.DatabaseNameMismatch, result.Status);
        Assert.False(probe.WasCalled);
    }

    [Fact]
    public async Task DiagnoseServerAsync_NotConfigured_RejectsWithoutAttemptingSqlConnection()
    {
        var probe = new FakeProbe { Result = new(RmsSqlProbeStatus.Reachable, "master") };
        var diagnostics = CreateDiagnostics(
            new RmsDatabaseConfiguration(false, RmsConnectionStringState.Unavailable, null, null, null),
            rawConnectionString: null,
            probe);

        var result = await diagnostics.DiagnoseServerAsync(RmsDatabaseKind.Branch);

        Assert.Equal(RmsDatabaseDiagnosticStatus.NotConfigured, result.Status);
        Assert.False(probe.WasCalled);
    }

    [Fact]
    public async Task DiagnoseServerAsync_MalformedConnectionString_ReportsConfigurationInvalidWithoutAttemptingSqlConnection()
    {
        var probe = new FakeProbe { Result = new(RmsSqlProbeStatus.Reachable, "master") };
        var diagnostics = CreateDiagnostics(
            ValidConfiguration("RmsBranchSrv"),
            rawConnectionString: "Data Source=.;Initial Catalog='unterminated;",
            probe);

        var result = await diagnostics.DiagnoseServerAsync(RmsDatabaseKind.Branch);

        Assert.Equal(RmsDatabaseDiagnosticStatus.ConfigurationInvalid, result.Status);
        Assert.False(probe.WasCalled);
    }

    [Fact]
    public async Task DiagnoseAsync_TargetDatabaseUnavailable_IsIndependentFromServerDiagnostic()
    {
        // The target-database diagnostic still requires the target catalog itself to answer, which
        // stays intentionally stricter than DiagnoseServerAsync used to preflight Restore.
        var probe = new FakeProbe { Result = new(RmsSqlProbeStatus.DatabaseUnavailable, null) };
        var diagnostics = CreateDiagnostics(ValidConfiguration("RmsBranchSrv"), "Server=.;Database=RmsBranchSrv;", probe);

        var result = await diagnostics.DiagnoseAsync(RmsDatabaseKind.Branch);

        Assert.Equal(RmsDatabaseDiagnosticStatus.DatabaseUnavailable, result.Status);
    }

    private static RmsDatabaseConfiguration ValidConfiguration(string databaseName) =>
        new(true, RmsConnectionStringState.Valid, "127.0.0.1,1433", databaseName, false);

    private static RmsDatabaseDiagnostics CreateDiagnostics(
        RmsDatabaseConfiguration configuration,
        string? rawConnectionString,
        FakeProbe probe) =>
        new(
            new FakeInstallationDiscovery(configuration),
            new FakeConnectionStringSource(rawConnectionString),
            probe,
            TimeProvider.System);

    private sealed class FakeInstallationDiscovery(RmsDatabaseConfiguration configuration) : IRmsInstallationDiscovery
    {
        public Task<RmsInstallationSnapshot> DiscoverAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new RmsInstallationSnapshot(
                true,
                true,
                true,
                "BR-1",
                "1",
                "1",
                "1",
                "installation-guid",
                "https://main.test",
                "branch.test",
                "Client",
                "2026.08",
                new(null, null, null),
                new(
                    RmsConsistencyState.Consistent,
                    RmsConsistencyState.Consistent,
                    RmsConsistencyState.Consistent,
                    RmsConsistencyState.Consistent,
                    RmsConsistencyState.Consistent,
                    []),
                configuration,
                configuration,
                new(RmsEndpointConfigurationState.Unavailable, null, null, null),
                new(RmsEndpointConfigurationState.Unavailable, null, null, null),
                new(true, true, true, true)));
    }

    private sealed class FakeConnectionStringSource(string? rawConnectionString) : IRmsDatabaseConnectionStringSource
    {
        public Task<string?> GetConnectionStringAsync(RmsDatabaseKind database, CancellationToken cancellationToken = default) =>
            Task.FromResult(rawConnectionString);
    }

    private sealed class FakeProbe : IRmsSqlReadOnlyProbe
    {
        public RmsSqlProbeResult Result { get; set; } = new(RmsSqlProbeStatus.Reachable, null);

        public bool WasCalled { get; private set; }

        public string? LastConnectionString { get; private set; }

        public Task<RmsSqlProbeResult> ProbeAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastConnectionString = connectionString;
            return Task.FromResult(Result);
        }
    }
}
