using System.Text.Json;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Databases;
using RmsSupportHub.Pos.Infrastructure.Installation;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

public sealed class RmsInstallationDiscoveryTests : IDisposable
{
    private const string BranchSecret = "synthetic-branch-secret-only";
    private const string CashierSecret = "synthetic-cashier-secret-only";
    private const string RmsInfoSecret = "synthetic-rms-info-secret-only";

    private readonly string _root = Directory.CreateTempSubdirectory("rms-installation-discovery-tests-").FullName;

    [Fact]
    public async Task MissingRmsInfo_DoesNotPreventBranchDiscovery()
    {
        WriteBranch();

        var snapshot = await DiscoverAsync();

        Assert.True(snapshot.InstallationDetected);
        Assert.True(snapshot.BranchInstalled);
        Assert.False(snapshot.CashierInstalled);
        Assert.Equal("P001", snapshot.BranchCode);
        Assert.Equal("RmsBranchSrv", snapshot.BranchDatabase.DatabaseName);
    }

    [Fact]
    public async Task MalformedRmsInfo_IsSafeAndDoesNotPreventCashierDiscovery()
    {
        WriteFile("RMSInfo.json", "{ malformed");
        WriteCashier();
        WriteCashierUi();

        var snapshot = await DiscoverAsync();

        Assert.True(snapshot.InstallationDetected);
        Assert.False(snapshot.BranchInstalled);
        Assert.True(snapshot.CashierInstalled);
        Assert.Equal("P001", snapshot.BranchCode);
        Assert.Equal("1", snapshot.PosNumber);
        Assert.Equal("RmsCashierSrv", snapshot.CashierDatabase.DatabaseName);
    }

    [Fact]
    public async Task BranchOnlyAndCashierOnlyComponentModesAreDetected()
    {
        WriteRmsInfo();
        WriteBranch();
        var branchOnly = await DiscoverAsync();
        Assert.True(branchOnly.BranchInstalled);
        Assert.False(branchOnly.CashierInstalled);

        ResetFiles();
        WriteRmsInfo();
        WriteCashier();
        WriteCashierUi();
        var cashierOnly = await DiscoverAsync();
        Assert.False(cashierOnly.BranchInstalled);
        Assert.True(cashierOnly.CashierInstalled);
    }

    [Fact]
    public async Task BranchAndCashierDiscoveryEvaluatesDuplicateMetadataAndRedactsSecrets()
    {
        WriteRmsInfo();
        WriteBranch();
        WriteCashier();
        WriteCashierUi();
        WriteServicesManager();

        var snapshot = await DiscoverAsync();
        var serialized = JsonSerializer.Serialize(snapshot);

        Assert.True(snapshot.BranchInstalled);
        Assert.True(snapshot.CashierInstalled);
        Assert.Equal(RmsConsistencyState.Consistent, snapshot.Consistency.BranchCode);
        Assert.Equal(RmsConsistencyState.Consistent, snapshot.Consistency.PosIdentity);
        Assert.Equal(RmsConsistencyState.Consistent, snapshot.Consistency.MainServerBranchId);
        Assert.Equal(RmsConsistencyState.Consistent, snapshot.Consistency.MainServerPosId);
        Assert.Equal(RmsConsistencyState.Consistent, snapshot.Consistency.Version);
        Assert.True(snapshot.Services.ServicesManagerInstalled);
        Assert.True(snapshot.Services.BranchServiceConfigured);
        Assert.True(snapshot.Services.CashierServiceConfigured);
        Assert.DoesNotContain(BranchSecret, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(CashierSecret, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(RmsInfoSecret, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiKey", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Data Source", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User ID", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateMetadataMismatchIsReportedWithoutFailingDiscovery()
    {
        WriteRmsInfo(branchCode: "P001", posNumber: "1", version: "5.7.4");
        WriteBranch(branchCode: "P002", version: "5.7.3");
        WriteCashier(branchCode: "P001", machineNo: "2", version: "5.7.4");
        WriteCashierUi(branchCode: "P001", version: "5.7.5");

        var snapshot = await DiscoverAsync();

        Assert.Equal(RmsConsistencyState.Mismatch, snapshot.Consistency.BranchCode);
        Assert.Equal(RmsConsistencyState.Mismatch, snapshot.Consistency.PosIdentity);
        Assert.Equal(RmsConsistencyState.Mismatch, snapshot.Consistency.Version);
        Assert.Contains(snapshot.Consistency.Warnings, warning => warning.Contains("Branch code", StringComparison.Ordinal));
        Assert.Contains(snapshot.Consistency.Warnings, warning => warning.Contains("POS identity", StringComparison.Ordinal));
        Assert.Contains(snapshot.Consistency.Warnings, warning => warning.Contains("versions", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddressLabelsNeverExposeUserInfo()
    {
        WriteRmsInfo(branchServerIp: "http://synthetic-user:synthetic-address-secret@branch.synthetic.test:5100");

        var snapshot = await DiscoverAsync();
        var serialized = JsonSerializer.Serialize(snapshot);

        Assert.Null(snapshot.BranchServerAddress);
        Assert.DoesNotContain("synthetic-address-secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-user", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CorrectDatabaseNameRunsOnlyTheFixedReadOnlyProbe()
    {
        WriteRmsInfo();
        WriteBranch();
        WriteCashier();
        var discovery = new RmsInstallationDiscovery(CreateOptions());
        var probe = new FakeSqlProbe(new(RmsSqlProbeStatus.Reachable, "RmsBranchSrv"));
        var diagnostics = new RmsDatabaseDiagnostics(
            discovery,
            discovery,
            probe,
            TimeProvider.System);

        var result = await diagnostics.DiagnoseAsync(RmsDatabaseKind.Branch);

        Assert.Equal(RmsDatabaseDiagnosticStatus.Reachable, result.Status);
        Assert.Equal("RmsBranchSrv", result.ConfiguredDatabase);
        Assert.Single(probe.ConnectionStrings);
        Assert.DoesNotContain(BranchSecret, result.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WrongDatabaseNameIsReportedWithoutOpeningSql()
    {
        WriteRmsInfo();
        WriteBranch(databaseName: "UnexpectedDatabase");
        var discovery = new RmsInstallationDiscovery(CreateOptions());
        var probe = new FakeSqlProbe(new(RmsSqlProbeStatus.Reachable, "UnexpectedDatabase"));
        var diagnostics = new RmsDatabaseDiagnostics(
            discovery,
            discovery,
            probe,
            TimeProvider.System);

        var result = await diagnostics.DiagnoseAsync(RmsDatabaseKind.Branch);

        Assert.Equal(RmsDatabaseDiagnosticStatus.DatabaseNameMismatch, result.Status);
        Assert.False(result.DatabaseNameMatches);
        Assert.Empty(probe.ConnectionStrings);
    }

    [Fact]
    public async Task CorrectCashierDatabaseRunsOnlyTheFixedReadOnlyProbe()
    {
        WriteRmsInfo();
        WriteCashier();
        var discovery = new RmsInstallationDiscovery(CreateOptions());
        var probe = new FakeSqlProbe(new(RmsSqlProbeStatus.Reachable, "RmsCashierSrv"));
        var diagnostics = new RmsDatabaseDiagnostics(
            discovery,
            discovery,
            probe,
            TimeProvider.System);

        var result = await diagnostics.DiagnoseAsync(RmsDatabaseKind.Cashier);

        Assert.Equal(RmsDatabaseDiagnosticStatus.Reachable, result.Status);
        Assert.Equal("RmsCashierSrv", result.ConfiguredDatabase);
        Assert.Single(probe.ConnectionStrings);
        Assert.DoesNotContain(CashierSecret, result.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WrongCashierDatabaseIsReportedWithoutOpeningSql()
    {
        WriteRmsInfo();
        WriteCashier(databaseName: "UnexpectedDatabase");
        var discovery = new RmsInstallationDiscovery(CreateOptions());
        var probe = new FakeSqlProbe(new(RmsSqlProbeStatus.Reachable, "UnexpectedDatabase"));
        var diagnostics = new RmsDatabaseDiagnostics(
            discovery,
            discovery,
            probe,
            TimeProvider.System);

        var result = await diagnostics.DiagnoseAsync(RmsDatabaseKind.Cashier);

        Assert.Equal(RmsDatabaseDiagnosticStatus.DatabaseNameMismatch, result.Status);
        Assert.False(result.DatabaseNameMatches);
        Assert.Empty(probe.ConnectionStrings);
    }

    [Fact]
    public async Task MissingAndMalformedConnectionStringsReturnSanitizedStatuses()
    {
        WriteRmsInfo();
        WriteFile("RMS.BranchServer/appsettings.json", "{\"ConnectionStrings\":{}}");
        var discovery = new RmsInstallationDiscovery(CreateOptions());
        var probe = new FakeSqlProbe(new(RmsSqlProbeStatus.Reachable, "RmsBranchSrv"));
        var diagnostics = new RmsDatabaseDiagnostics(discovery, discovery, probe, TimeProvider.System);

        var missing = await diagnostics.DiagnoseAsync(RmsDatabaseKind.Branch);
        Assert.Equal(RmsDatabaseDiagnosticStatus.NotConfigured, missing.Status);

        ResetFiles();
        WriteFile("RMS.BranchServer/appsettings.json", "{ malformed");
        discovery = new RmsInstallationDiscovery(CreateOptions());
        diagnostics = new RmsDatabaseDiagnostics(discovery, discovery, probe, TimeProvider.System);
        var malformedJson = await diagnostics.DiagnoseAsync(RmsDatabaseKind.Branch);
        Assert.Equal(RmsDatabaseDiagnosticStatus.ConfigurationInvalid, malformedJson.Status);
        Assert.True(malformedJson.Configured);

        ResetFiles();
        WriteBranch(rawConnectionString: "not-a-sql-connection-string");
        discovery = new RmsInstallationDiscovery(CreateOptions());
        diagnostics = new RmsDatabaseDiagnostics(discovery, discovery, probe, TimeProvider.System);
        var malformed = await diagnostics.DiagnoseAsync(RmsDatabaseKind.Branch);
        Assert.Equal(RmsDatabaseDiagnosticStatus.ConfigurationInvalid, malformed.Status);
        Assert.Empty(probe.ConnectionStrings);
    }

    private async Task<RmsInstallationSnapshot> DiscoverAsync()
    {
        var discovery = new RmsInstallationDiscovery(CreateOptions());
        return await discovery.DiscoverAsync();
    }

    private RmsInstallationOptions CreateOptions() => new()
    {
        RmsInfoPath = Path.Combine(_root, "RMSInfo.json"),
        BranchServerSettingsPath = Path.Combine(_root, "RMS.BranchServer", "appsettings.json"),
        CashierServerSettingsPath = Path.Combine(_root, "RMS.CashierServer", "appsettings.json"),
        CashierUiSettingsPath = Path.Combine(_root, "RMS.CashierUI", "appsettings.json"),
        ServicesManagerSettingsPath = Path.Combine(_root, "RMSServicesManager", "appsettings.json"),
        BranchServerDirectory = Path.Combine(_root, "RMS.BranchServer"),
        CashierServerDirectory = Path.Combine(_root, "RMS.CashierServer"),
        CashierUiDirectory = Path.Combine(_root, "RMS.CashierUI"),
        ServicesManagerDirectory = Path.Combine(_root, "RMSServicesManager")
    };

    private void WriteRmsInfo(
        string branchCode = "P001",
        string posNumber = "1",
        string version = "5.7.4",
        string branchServerIp = "branch.synthetic.test") =>
        WriteFile(
            "RMSInfo.json",
            $$"""
            {
              "BranchCode": "{{branchCode}}",
              "POSNumber": {{posNumber}},
              "MainServerBranchId": 1,
              "MainServerPosId": 1,
              "MainServerUrl": "http://main.synthetic.test:8080/api",
              "BranchServerIP": "{{branchServerIp}}",
              "UninstallGUID": "synthetic-installation-guid",
              "BuildNumber": "{{version}}",
              "Password": "{{RmsInfoSecret}}"
            }
            """);

    private void WriteBranch(
        string branchCode = "P001",
        string version = "5.7.4",
        string databaseName = "RmsBranchSrv",
        string? rawConnectionString = null) =>
        WriteFile(
            "RMS.BranchServer/appsettings.json",
            $$"""
            {
              "BranchSettings": {
                "BranchCode": "{{branchCode}}",
                "MainBaseUrl": "http://main.synthetic.test:8080/api",
                "MainServerBranchId": 1,
                "InstallationGuid": "synthetic-installation-guid"
              },
              "BuildNumber": "{{version}}",
              "ConnectionStrings": {
                "BranchServer": "{{rawConnectionString ?? $"Data Source=sql.synthetic.test,1433;Database={databaseName};User ID=synthetic-reader;Password={BranchSecret};TrustServerCertificate=True"}}"
              },
              "ApiKey": "synthetic-api-key"
            }
            """);

    private void WriteCashier(
        string branchCode = "P001",
        string machineNo = "1",
        string version = "5.7.4",
        string databaseName = "RmsCashierSrv") =>
        WriteFile(
            "RMS.CashierServer/appsettings.json",
            $$"""
            {
              "PosBasicInfoSettings": {
                "MachineNo": {{machineNo}},
                "MachineName": "POS-{{machineNo}}",
                "BranchCode": "{{branchCode}}",
                "BranchName": "Synthetic Branch",
                "BranchBaseUrl": "http://branch.synthetic.test:5100/",
                "MainServerBaseUrl": "http://main.synthetic.test:8080/api",
                "MainServerPosId": 1,
                "MainServerBranchId": 1
              },
              "BuildNumber": "{{version}}",
              "ConnectionStrings": {
                "RmsPos": "Data Source=sql.synthetic.test,1433;Database={{databaseName}};User ID=synthetic-reader;Password={{CashierSecret}};TrustServerCertificate=True"
              },
              "EncryptionKey": "synthetic-encryption-key"
            }
            """);

    private void WriteCashierUi(string branchCode = "P001", string version = "5.7.4") =>
        WriteFile(
            "RMS.CashierUI/appsettings.json",
            $$"""
            {
              "GrpcServer": {
                "MachineName": "POS-1",
                "BranchCode": "{{branchCode}}",
                "BranchName": "Synthetic Branch",
                "BranchBaseUrl": "http://branch.synthetic.test:5100/",
                "gRPCCashierServiceUrl": "http://localhost:5115/",
                "Url": "http://localhost:5117"
              },
              "Settings": {
                "TheClient": "UPC",
                "BuildNumber": "{{version}}"
              }
            }
            """);

    private void WriteServicesManager() =>
        WriteFile(
            "RMSServicesManager/appsettings.json",
            "{\"Services\":[{\"ServiceName\":\"RMS.BranchService\"},{\"ServiceName\":\"RMS.CashierService\"}]}" );

    private void WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private void ResetFiles()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FakeSqlProbe(RmsSqlProbeResult result) : IRmsSqlReadOnlyProbe
    {
        public List<string> ConnectionStrings { get; } = [];

        public Task<RmsSqlProbeResult> ProbeAsync(
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConnectionStrings.Add(connectionString);
            return Task.FromResult(result);
        }
    }
}
