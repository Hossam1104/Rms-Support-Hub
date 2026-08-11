using System.Collections.Concurrent;
using RmsSupportHub.Pos.Application.Maintenance;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

public sealed class MaintenancePathPolicyTests
{
    // The policy protects Windows Agent paths. Keep the fixture independent of the host OS and
    // avoid using whichever TEMP/TMP variables happen to exist on a CI runner.
    private readonly string _root = @"C:\ProgramData\RMS_Plus\TestData\pos-maintenance-policy-root";
    private readonly FakeFileSystem _fileSystem = new();

    [Fact]
    public void ContainmentAndRootPolicyRejectTraversalAbsoluteProtectedInstallAndRootTargets()
    {
        var settings = PathSettings();
        var policy = new MaintenancePathPolicy(_fileSystem);

        Assert.Equal(MaintenanceFailureCodes.OutsideManagedRoot, policy.Resolve("one", Join(_root, "..", "outside"), settings).RejectionCode);
        Assert.Equal(MaintenanceFailureCodes.OutsideManagedRoot, policy.Resolve("two", @"C:\ProgramData\RMS_Plus\unmanaged", settings).RejectionCode);
        Assert.Equal(MaintenanceFailureCodes.ProtectedRoot, policy.Resolve("three", Join(_root, "protected", "child"), settings).RejectionCode);
        Assert.Equal(MaintenanceFailureCodes.InstallRoot, policy.Resolve("four", Join(_root, "install", "child"), settings).RejectionCode);
        Assert.Equal(MaintenanceFailureCodes.RootTarget, policy.Resolve("five", _root, settings).RejectionCode);
    }

    [Fact]
    public void ProtectedAndInstallRootsRejectEveryContainmentOverlapButAllowSafeSiblings()
    {
        var settings = PathSettings();
        var policy = new MaintenancePathPolicy(_fileSystem);
        var protectedRoot = settings.ProtectedRoots.Single();
        var installRoot = settings.InstallRoots.Single();
        var protectedParentSettings = PathSettings();
        protectedParentSettings.ProtectedRoots = [Join(_root, "managed", "protected")];
        protectedParentSettings.InstallRoots = [Join(_root, "other-install")];
        var installParentSettings = PathSettings();
        installParentSettings.ProtectedRoots = [Join(_root, "other-protected")];
        installParentSettings.InstallRoots = [Join(_root, "managed", "install")];

        Assert.Equal(
            MaintenanceFailureCodes.ProtectedRoot,
            policy.Resolve("protected-child", Join(protectedRoot, "child"), settings).RejectionCode);
        Assert.Equal(
            MaintenanceFailureCodes.ProtectedRoot,
            policy.Resolve("protected-equal", protectedRoot, settings).RejectionCode);
        Assert.Equal(
            MaintenanceFailureCodes.ProtectedRoot,
            policy.Resolve("protected-parent", Join(_root, "managed"), protectedParentSettings).RejectionCode);
        Assert.Equal(
            MaintenanceFailureCodes.InstallRoot,
            policy.Resolve("install-parent", Join(_root, "managed"), installParentSettings).RejectionCode);
        Assert.True(policy.Resolve("safe-sibling", Join(_root, "safe-sibling"), settings).Accepted);
    }

    [Theory]
    [InlineData("managed")]
    [InlineData("data")]
    [InlineData("protected")]
    [InlineData("install")]
    public void EmptyRequiredSafetyRootsFailClosed(string missingRoot)
    {
        var settings = PathSettings();
        switch (missingRoot)
        {
            case "managed": settings.ManagedRoots = []; break;
            case "data": settings.DataRoots = []; break;
            case "protected": settings.ProtectedRoots = []; break;
            case "install": settings.InstallRoots = []; break;
        }

        var result = new MaintenancePathPolicy(_fileSystem).Resolve(
            "missing-root",
            Join(_root, "safe-sibling"),
            settings);

        Assert.False(result.Accepted);
        Assert.NotNull(result.RejectionCode);
        Assert.DoesNotContain(_root, result.SafeMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidRequiredSafetyRootFailsClosed()
    {
        var settings = PathSettings();
        settings.DataRoots = ["C:relative-data-root"];

        var result = new MaintenancePathPolicy(_fileSystem).Resolve(
            "invalid-data-root",
            Join(_root, "safe-sibling"),
            settings);

        Assert.Equal(MaintenanceFailureCodes.InvalidConfiguration, result.RejectionCode);
        Assert.DoesNotContain(_root, result.SafeMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DriveRelativeUncAndUnresolvedEnvironmentPathsFailClosed()
    {
        var settings = PathSettings();
        var policy = new MaintenancePathPolicy(_fileSystem);

        Assert.Equal(MaintenanceFailureCodes.DriveRelativePath, policy.Resolve("drive", "C:relative", settings).RejectionCode);
        Assert.Equal(MaintenanceFailureCodes.UncNotAllowed, policy.Resolve("unc", @"\\server\share\folder", settings).RejectionCode);
        Assert.Equal(MaintenanceFailureCodes.UnresolvedEnvironmentVariable, policy.Resolve("env", "%POS_MISSING_MAINTENANCE_ROOT%\target", settings).RejectionCode);

        settings.AllowUncPaths = true;
        Assert.Equal(MaintenanceFailureCodes.OutsideManagedRoot, policy.Resolve("unc-allowed", @"\\server\share\folder", settings).RejectionCode);
    }

    [Fact]
    public void EnvironmentExpansionThatEscapesManagedRootIsRejected()
    {
        var settings = PathSettings();
        settings.ManagedRoots = ["%POS_TEST_ROOT%\\pos-maintenance-policy-root"];
        var policy = new MaintenancePathPolicy(_fileSystem);

        var result = policy.Resolve("escape", "%POS_TEST_ROOT%\\outside", settings);

        Assert.Equal(MaintenanceFailureCodes.OutsideManagedRoot, result.RejectionCode);
    }

    [Fact]
    public void WindowsPathComparisonIsCaseInsensitiveAndRespectsRootBoundaries()
    {
        var settings = PathSettings();
        var policy = new MaintenancePathPolicy(_fileSystem);

        Assert.True(policy.Resolve(
            "mixed",
            @"c:/PROGRAMDATA/RMS_PLUS/TESTDATA/POS-MAINTENANCE-POLICY-ROOT/safe-sibling",
            settings).Accepted);

        Assert.Equal(
            MaintenanceFailureCodes.OutsideManagedRoot,
            policy.Resolve("prefix-collision", _root + "2\\child", settings).RejectionCode);

        Assert.Equal(
            MaintenanceFailureCodes.ProtectedRoot,
            policy.Resolve("dot-segment", Join(_root, "safe", "..", "protected", "child"), settings).RejectionCode);
    }

    [Fact]
    public void InvalidPolicyRootsFailClosedInsteadOfBeingIgnored()
    {
        var settings = PathSettings();
        settings.ProtectedRoots = [Join(_root, "protected"), "C:relative-protected"];
        var policy = new MaintenancePathPolicy(_fileSystem);

        var result = policy.Resolve("invalid-policy", Join(_root, "target"), settings);

        Assert.Equal(MaintenanceFailureCodes.InvalidConfiguration, result.RejectionCode);
    }

    [Fact]
    public void ReparseAndSymlinkEscapesAreRejectedEvenWhenTheLinkNameIsUnderTheManagedRoot()
    {
        var settings = PathSettings();
        var target = Join(_root, "link");
        _fileSystem.SetReparse(target, @"C:\ProgramData\RMS_Plus\outside-link-target");
        _fileSystem.SetAncestors(target, [_fileSystem.Inspect(target)]);
        var policy = new MaintenancePathPolicy(_fileSystem);

        var rejected = policy.Resolve("link", target, settings);
        Assert.Equal(MaintenanceFailureCodes.ReparsePoint, rejected.RejectionCode);

        settings.RejectReparsePoints = false;
        var escaped = policy.Resolve("link", target, settings);
        Assert.Equal(MaintenanceFailureCodes.ReparseEscape, escaped.RejectionCode);
    }

    [Fact]
    public void ReparseDestinationInsideOrContainingProtectedRootIsRejected()
    {
        var settings = PathSettings();
        settings.RejectReparsePoints = false;
        var policy = new MaintenancePathPolicy(_fileSystem);
        var target = Join(_root, "reparse-target");
        var protectedRoot = settings.ProtectedRoots.Single();

        _fileSystem.SetReparse(target, Join(protectedRoot, "destination"));
        _fileSystem.SetAncestors(target, [_fileSystem.Inspect(target)]);
        Assert.Equal(MaintenanceFailureCodes.ReparseEscape, policy.Resolve("reparse-inside", target, settings).RejectionCode);

        _fileSystem.SetReparse(target, _root);
        _fileSystem.SetAncestors(target, [_fileSystem.Inspect(target)]);
        Assert.Equal(MaintenanceFailureCodes.ReparseEscape, policy.Resolve("reparse-containing", target, settings).RejectionCode);
    }

    [Fact]
    public async Task CleanupRecomputesTheTargetAndFailsClosedWhenConfigurationChanges()
    {
        var settings = ApplicationSettings();
        settings.Maintenance.CleanupTargets = [Join(_root, "first")];
        _fileSystem.SetEntry(settings.Maintenance.CleanupTargets[0]);
        var service = new MaintenanceService(new FakeDatabase(), new FakeServices(), _fileSystem);
        var preview = await service.BuildCleanupPreviewAsync(settings);

        settings.Maintenance.CleanupTargets = [Join(_root, "changed")];
        _fileSystem.SetEntry(settings.Maintenance.CleanupTargets[0]);
        var execution = await service.ExecuteCleanupAsync(settings, preview.Intent!.Fingerprint);

        Assert.Equal(OperationStatus.Failed, execution.Operation.Status);
        Assert.Equal(MaintenanceFailureCodes.PreviewChanged, execution.FailureCode);
        Assert.Empty(_fileSystem.DeleteCalls);
    }

    [Fact]
    public async Task CleanupRecordsPartialResidueAfterADeleteFailure()
    {
        var settings = ApplicationSettings();
        settings.Maintenance.CleanupTargets = [Join(_root, "first"), Join(_root, "second")];
        _fileSystem.SetEntry(settings.Maintenance.CleanupTargets[0]);
        _fileSystem.SetEntry(settings.Maintenance.CleanupTargets[1]);
        _fileSystem.DeleteFailure = new IOException("private path and secret connection string");
        var service = new MaintenanceService(new FakeDatabase(), new FakeServices(), _fileSystem);

        var execution = await service.ExecuteCleanupAsync(settings);

        Assert.Equal(OperationStatus.PartialSuccess, execution.Operation.Status);
        Assert.True(execution.Evidence.RecoveryRequired);
        Assert.Contains(execution.Evidence.Items, item => item.Attempted && item.ResidueUncertain && item.FailureCode == MaintenanceFailureCodes.TargetDeleteFailed);
        Assert.DoesNotContain("private path", string.Join(" ", execution.Operation.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BranchResetUsesConfiguredLogicalBranchWithoutBareBakParsing()
    {
        var settings = ApplicationSettings();
        settings.BranchCode = "NORTH_EU_01";
        settings.Maintenance.BranchResetDatabase = "BranchData";
        var database = new FakeDatabase();
        var service = new MaintenanceService(database, new FakeServices(), _fileSystem);

        var preview = await service.BuildBranchResetPreviewAsync(settings);
        var execution = await service.ExecuteBranchResetAsync(settings, preview.Intent!.Fingerprint);

        Assert.True(preview.Ready);
        Assert.Equal("RESET BRANCH NORTH_EU_01", preview.Intent!.ConfirmationText);
        Assert.Equal(OperationStatus.Success, execution.Operation.Status);
        Assert.Equal("BranchData", database.ResetCalls.Single().DatabaseName);
        Assert.Equal("NORTH_EU_01", database.ResetCalls.Single().BranchCode);
        Assert.Equal(2, database.BranchVerificationDatabases.Count);
        Assert.All(database.BranchVerificationDatabases, name => Assert.Equal("BranchData", name));
    }

    [Fact]
    public async Task BranchResetUsesResolvedDatabaseByDefaultAndNormalizesApprovedTableSubset()
    {
        var settings = ApplicationSettings();
        settings.Databases = ["RmsBranchSrv"];
        settings.BranchCode = "NORTH_EU_03";
        settings.Maintenance.BranchResetDatabase = string.Empty;
        settings.Maintenance.BranchResetTables = ["sales", "SALES", "CashierSessions"];
        var database = new FakeDatabase();
        var service = new MaintenanceService(database, new FakeServices(), _fileSystem);

        var preview = await service.BuildBranchResetPreviewAsync(settings);
        var execution = await service.ExecuteBranchResetAsync(settings, preview.Intent!.Fingerprint);

        Assert.True(preview.Ready);
        Assert.Equal("RmsBranchSrv", preview.Intent.DatabaseName);
        Assert.Equal(["Sales", "CashierSessions"], preview.Intent.TableNames);
        Assert.Equal(2, database.BranchVerificationDatabases.Count);
        Assert.All(database.BranchVerificationDatabases, name => Assert.Equal("RmsBranchSrv", name));
        Assert.Equal(["Sales", "CashierSessions"], database.ResetCalls.Single().Tables);
        Assert.Equal(OperationStatus.Success, execution.Operation.Status);
    }

    [Fact]
    public async Task UnrelatedDatabaseIsRejectedBeforeAnyReset()
    {
        var settings = ApplicationSettings();
        settings.Databases = ["RmsBranchSrv"];
        settings.Maintenance.BranchResetDatabase = "UnrelatedDatabase";
        var database = new FakeDatabase();
        var service = new MaintenanceService(database, new FakeServices(), _fileSystem);

        var preview = await service.BuildBranchResetPreviewAsync(settings);
        var execution = await service.ExecuteBranchResetAsync(settings);

        Assert.False(preview.Ready);
        Assert.Contains(preview.Rejections, item => item.Code == MaintenanceFailureCodes.DatabaseOutOfScope);
        Assert.Empty(database.BranchVerificationDatabases);
        Assert.Empty(database.ResetCalls);
        Assert.Equal(OperationStatus.Failed, execution.Operation.Status);
        Assert.False(execution.Evidence.DestructiveAttempted);
    }

    [Fact]
    public async Task UnknownConfiguredTableIsRejectedBeforeAnyReset()
    {
        var settings = ApplicationSettings();
        settings.Databases = ["RmsBranchSrv"];
        settings.Maintenance.BranchResetTables = ["Sales", "CustomerBalances"];
        var database = new FakeDatabase();
        var service = new MaintenanceService(database, new FakeServices(), _fileSystem);

        var preview = await service.BuildBranchResetPreviewAsync(settings);
        var execution = await service.ExecuteBranchResetAsync(settings);

        Assert.False(preview.Ready);
        Assert.Contains(preview.Rejections, item => item.TargetId == "tables");
        Assert.Empty(database.BranchVerificationDatabases);
        Assert.Empty(database.ResetCalls);
        Assert.Equal(OperationStatus.Failed, execution.Operation.Status);
        Assert.False(execution.Evidence.DestructiveAttempted);
    }

    [Fact]
    public async Task ExactTargetDatabaseVerificationFailurePreventsReset()
    {
        var settings = ApplicationSettings();
        settings.Databases = ["RmsBranchSrv"];
        var database = new FakeDatabase
        {
            BranchVerificationFailure = new IOException("secret=C:\\private\\database"),
        };
        var service = new MaintenanceService(database, new FakeServices(), _fileSystem);

        var preview = await service.BuildBranchResetPreviewAsync(settings);
        var execution = await service.ExecuteBranchResetAsync(settings);

        Assert.False(preview.Ready);
        Assert.Contains(preview.Rejections, item => item.Code == MaintenanceFailureCodes.DatabaseScopeUnavailable);
        Assert.Equal(2, database.BranchVerificationDatabases.Count);
        Assert.All(database.BranchVerificationDatabases, name => Assert.Equal("RmsBranchSrv", name));
        Assert.Empty(database.ResetCalls);
        Assert.Equal(OperationStatus.Failed, execution.Operation.Status);
        Assert.DoesNotContain("private", string.Join(" ", preview.Rejections.Select(item => item.Message)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancellationBeforeTheResetSeamIsCancelledWithoutDestructiveAttempt()
    {
        var settings = ApplicationSettings();
        settings.BranchCode = "NORTH_EU_02";
        settings.Maintenance.BranchResetDatabase = "BranchData";
        var database = new FakeDatabase();
        var service = new MaintenanceService(database, new FakeServices(), _fileSystem);
        var preview = await service.BuildBranchResetPreviewAsync(settings);
        using var cancellation = new CancellationTokenSource();
        database.OnBranchExists = cancellation.Cancel;

        var execution = await service.ExecuteBranchResetAsync(
            settings,
            preview.Intent!.Fingerprint,
            cancellationToken: cancellation.Token);

        Assert.Equal(OperationStatus.Cancelled, execution.Operation.Status);
        Assert.False(execution.Evidence.DestructiveAttempted);
        Assert.Empty(execution.Evidence.Items);
        Assert.Empty(database.ResetCalls);
    }

    private MaintenanceSettings PathSettings()
    {
        return new MaintenanceSettings
        {
            ManagedRoots = [_root],
            DataRoots = [_root],
            ProtectedRoots = [Join(_root, "protected")],
            InstallRoots = [Join(_root, "install")],
        };
    }

    private AppSettings ApplicationSettings() => new()
    {
        BranchCode = "NORTH_EU_01",
        Services = ["TestService"],
        Databases = ["BranchData"],
        Maintenance = PathSettings(),
    };

    private sealed class FakeFileSystem : IMaintenanceFileSystem
    {
        private readonly ConcurrentDictionary<string, MaintenancePathInspection> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, IReadOnlyList<MaintenancePathInspection>> _ancestorEntries = new(StringComparer.OrdinalIgnoreCase);

        public ConcurrentQueue<string> DeleteCalls { get; } = new();
        public Exception? DeleteFailure { get; set; }
        public string ExpandEnvironmentVariables(string path) =>
            path.Replace("%POS_TEST_ROOT%", @"C:\ProgramData\RMS_Plus\TestData\pos-maintenance-policy-root", StringComparison.Ordinal);

        public string GetFullPath(string path) => Normalize(path);
        public MaintenancePathInspection Inspect(string path) => _entries.GetValueOrDefault(GetFullPath(path), new(GetFullPath(path), false, false, false, null, null, null));
        public IReadOnlyList<MaintenancePathInspection> InspectAncestors(string path) => _ancestorEntries.GetValueOrDefault(GetFullPath(path), [Inspect(path)]);
        public long? TryGetAvailableFreeSpace(string path) => 1024;
        public Task DeleteAsync(string path, bool recursive, CancellationToken cancellationToken = default)
        {
            DeleteCalls.Enqueue(path);
            if (DeleteFailure is not null) return Task.FromException(DeleteFailure);
            return Task.CompletedTask;
        }
        public void SetEntry(string path) => _entries[GetFullPath(path)] = new(GetFullPath(path), true, true, false, null, null, 1);
        public void SetReparse(string path, string target) => _entries[GetFullPath(path)] = new(GetFullPath(path), true, true, true, target, null, 1);
        public void SetAncestors(string path, IReadOnlyList<MaintenancePathInspection> entries) => _ancestorEntries[GetFullPath(path)] = entries;

        private static string Normalize(string path) => path.Replace('/', '\\').TrimEnd('\\');
    }

    private static string Join(params string[] parts)
    {
        var result = parts[0].TrimEnd('\\', '/');
        foreach (var part in parts.Skip(1))
        {
            result += "\\" + part.Trim('\\', '/');
        }

        return result;
    }

    private sealed class FakeServices : IServiceManager
    {
        public Task<ServiceStatus> GetStatusAsync(string serviceName, CancellationToken cancellationToken = default) => Task.FromResult(ServiceStatus.Running);
        public Task<IReadOnlyDictionary<string, ServiceStatus>> GetStatusesAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<string, ServiceStatus>>(serviceNames.ToDictionary(item => item, _ => ServiceStatus.Running));
        public Task ControlAsync(string serviceName, ServiceControlAction action, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDatabase : IDatabaseService, IMaintenanceDatabasePreview, IMaintenanceDatabaseReset
    {
        public List<(string DatabaseName, string BranchCode, IReadOnlyList<string> Tables)> ResetCalls { get; } = [];
        public List<string> BranchVerificationDatabases { get; } = [];
        public Action? OnBranchExists { get; set; }
        public Exception? BranchVerificationFailure { get; set; }
        public Task TestConnectionAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> BranchExistsAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default)
        {
            OnBranchExists?.Invoke();
            return Task.FromResult(true);
        }
        public Task<bool> BranchExistsInDatabaseAsync(AppSettings settings, string databaseName, string branchCode, CancellationToken cancellationToken = default)
        {
            BranchVerificationDatabases.Add(databaseName);
            OnBranchExists?.Invoke();
            if (BranchVerificationFailure is not null) return Task.FromException<bool>(BranchVerificationFailure);
            return Task.FromResult(true);
        }
        public Task<IReadOnlyList<MaintenanceTableScope>> GetBranchResetScopeAsync(
            AppSettings settings,
            string databaseName,
            string branchCode,
            IReadOnlyList<string> tableNames,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MaintenanceTableScope>>(
                tableNames.Select(table => new MaintenanceTableScope(table, null)).ToList());
        public Task ResetBranchDataAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResetBranchDataAsync(AppSettings settings, string databaseName, string branchCode, IReadOnlyList<string> tableNames, CancellationToken cancellationToken = default)
        {
            ResetCalls.Add((databaseName, branchCode, tableNames));
            return Task.CompletedTask;
        }
        public Task BackupDatabaseAsync(AppSettings settings, string databaseName, string backupFilePath, bool useCompatibilityMode, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RestoreFileInfo>> ReadRestoreFileListAsync(AppSettings settings, string backupFilePath, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RestoreFileInfo>>([]);
        public Task RestoreDatabaseAsync(AppSettings settings, string targetDatabase, string backupFilePath, IReadOnlyList<RestoreFileInfo> logicalFiles, string dbFilesPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
