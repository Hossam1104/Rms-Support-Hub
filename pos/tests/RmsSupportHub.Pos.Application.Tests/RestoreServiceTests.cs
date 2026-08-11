using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RmsSupportHub.Pos.Application.Restore;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

public sealed class RestoreServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pos-restore-tests-").FullName;

    [Fact]
    public async Task ValidLegacyAndManifestArchives_ExecuteWithFakesOnly()
    {
        var database = new FakeDatabaseService
        {
            RestoreFileList = [new RestoreFileInfo("branch_data", "D"), new RestoreFileInfo("branch_log", "L")],
        };
        var services = new FakeServiceManager();
        services.Statuses["TestService"] = ServiceStatus.Running;
        var settings = CreateSettings();
        var service = CreateService(database, services);

        var legacy = CreateArchive(
            "B001_legacy.zip",
            [("B001_branch.bak", Bytes("legacy"))]);
        var legacyPreview = await service.BuildPreviewAsync(settings, Source(legacy, "B001_legacy.zip"), RestoreMode.DatabaseOnly);
        Assert.True(legacyPreview.Ready, legacyPreview.SafeMessage);
        Assert.Equal("legacy-no-manifest-v1", legacyPreview.Intent!.ArchiveVersion);
        var legacyExecution = await service.ExecuteAsync(settings, Source(legacy, "B001_legacy.zip"), RestoreMode.DatabaseOnly, legacyPreview.Intent.Fingerprint);
        Assert.Equal(OperationStatus.Success, legacyExecution.Operation.Status);
        Assert.True(database.RestoreAttempted);
        Assert.True(database.RestoreCompleted);

        var bareBak = Path.Combine(_root, "B001_device.bak");
        await File.WriteAllTextAsync(bareBak, "device-side backup");
        var barePreview = await service.BuildPreviewAsync(settings, Source(bareBak, "B001_device.bak"), RestoreMode.DatabaseOnly);
        Assert.True(barePreview.Ready, barePreview.SafeMessage);
        Assert.Equal("bare-bak-v1", barePreview.Intent!.ArchiveVersion);
        var bareExecution = await service.ExecuteAsync(settings, Source(bareBak, "B001_device.bak"), RestoreMode.DatabaseOnly, barePreview.Intent.Fingerprint);
        Assert.Equal(OperationStatus.Success, bareExecution.Operation.Status);

        var manifest = CreateArchive(
            "B001_manifest.zip",
            [
                ("B001_branch.bak", Bytes("new")),
                ("RMS_BranchService_appsettings.json", Bytes("{\"branch\":\"restored\"}")),
            ],
            includeManifest: true);
        var manifestPreview = await service.BuildPreviewAsync(settings, Source(manifest, "B001_manifest.zip"), RestoreMode.Full);
        Assert.True(manifestPreview.Ready, manifestPreview.SafeMessage);
        Assert.Equal("manifest-v1", manifestPreview.Intent!.ArchiveVersion);
        var manifestExecution = await service.ExecuteAsync(settings, Source(manifest, "B001_manifest.zip"), RestoreMode.Full, manifestPreview.Intent.Fingerprint);

        Assert.Equal(OperationStatus.Success, manifestExecution.Operation.Status);
        Assert.Equal(3, database.RestoreCalls.Count);
        Assert.Equal("{\"branch\":\"restored\"}", await File.ReadAllTextAsync(settings.BranchConfigPath));
        Assert.Contains(("TestService", ServiceControlAction.Stop), services.Controls);
        Assert.Contains(("TestService", ServiceControlAction.Start), services.Controls);
        AssertNoStagingResidue(Path.Combine(_root, "staging"));
    }

    [Theory]
    [InlineData("B001_device.bak", null)]
    [InlineData("B002_device.bak", "restore.archive_branch_mismatch")]
    [InlineData("backup_final.bak", "restore.branch_evidence_missing")]
    [InlineData("B001_B002_device.bak", "restore.archive_bak_ambiguous")]
    public async Task BareBakRequiresPositiveUnambiguousBranchEvidence(string fileName, string? expectedCode)
    {
        var path = Path.Combine(_root, fileName);
        await File.WriteAllTextAsync(path, "device-side backup");

        var result = await CreateService().BuildPreviewAsync(
            CreateSettings(),
            Source(path, fileName),
            RestoreMode.DatabaseOnly);

        if (expectedCode is null)
        {
            Assert.True(result.Ready, result.SafeMessage);
            return;
        }

        AssertRejected(result, expectedCode);
        Assert.DoesNotContain(_root, result.SafeMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../B001_branch.bak", "restore.archive_path_rejected")]
    [InlineData("/B001_branch.bak", "restore.archive_path_rejected")]
    [InlineData("C:\\B001_branch.bak", "restore.archive_path_rejected")]
    [InlineData("B001_branch.txt", "restore.archive_extension_rejected")]
    public async Task ArchivePathAndExtensionPolicyRejectsBeforeExtraction(string entryName, string expectedCode)
    {
        var archive = CreateArchive("invalid.zip", [(entryName, Bytes("bad"))]);
        var service = CreateService();

        var result = await service.BuildPreviewAsync(CreateSettings(), Source(archive, "invalid.zip"), RestoreMode.Full);

        AssertRejected(result, expectedCode);
        AssertNoStagingResidue(Path.Combine(_root, "staging"));
    }

    [Fact]
    public async Task ReparseSourceAndExtractedContentAreRejected()
    {
        var archive = CreateArchive("B001_reparse.zip", [("B001_branch.bak", Bytes("bad"))]);
        var sourcePath = Path.GetFullPath(archive);
        var sourceFs = new TestRestoreFileSystem(path => string.Equals(Path.GetFullPath(path), sourcePath, StringComparison.OrdinalIgnoreCase));
        var sourceResult = await CreateService(fileSystem: sourceFs).BuildPreviewAsync(
            CreateSettings(), Source(archive, "B001_reparse.zip"), RestoreMode.Full);
        AssertRejected(sourceResult, "restore.source_invalid");

        var stagingRoot = Path.Combine(_root, "staging-reparse");
        var extractionFs = new TestRestoreFileSystem(path =>
            path.StartsWith(Path.GetFullPath(stagingRoot), StringComparison.OrdinalIgnoreCase)
            && Path.GetExtension(path).Equals(".bak", StringComparison.OrdinalIgnoreCase)
            && File.Exists(path));
        var extractionResult = await CreateService(fileSystem: extractionFs, stagingRoot: stagingRoot).BuildPreviewAsync(
            CreateSettings(), Source(archive, "B001_reparse.zip"), RestoreMode.Full);
        AssertRejected(extractionResult, "restore.archive_path_rejected");
        Assert.Empty(Directory.EnumerateFileSystemEntries(stagingRoot));

        var symlinkArchive = CreateSymlinkArchive("B001_symlink.zip");
        var symlinkResult = await CreateService().BuildPreviewAsync(
            CreateSettings(), Source(symlinkArchive, "B001_symlink.zip"), RestoreMode.Full);
        AssertRejected(symlinkResult, "restore.archive_path_rejected");
    }

    [Fact]
    public async Task ArchiveResourceLimitsRejectRatioEntryCountAndExpandedBytes()
    {
        var ratioArchive = CreateArchive(
            "B001_ratio.zip",
            [("B001_branch.bak", Enumerable.Repeat((byte)'A', 256 * 1024).ToArray())],
            compression: CompressionLevel.Optimal);
        var ratioResult = await CreateService(limits: new RestoreArchiveLimits { MaxCompressionRatio = 2 }).BuildPreviewAsync(
            CreateSettings(), Source(ratioArchive, "B001_ratio.zip"), RestoreMode.Full);
        AssertRejected(ratioResult, "restore.archive_compression_ratio");

        var tooMany = CreateArchive(
            "B001_many.zip",
            [
                ("B001_branch.bak", Bytes("bak")),
                ("branch-config.json", Bytes("{}")),
                ("cashier-server-config.json", Bytes("{}")),
            ]);
        var countResult = await CreateService(limits: new RestoreArchiveLimits { MaxArchiveEntryCount = 2 }).BuildPreviewAsync(
            CreateSettings(), Source(tooMany, "B001_many.zip"), RestoreMode.Full);
        AssertRejected(countResult, "restore.archive_entry_limit");

        var expanded = CreateArchive("B001_expanded.zip", [("B001_branch.bak", Enumerable.Repeat((byte)'B', 32).ToArray())]);
        var expandedResult = await CreateService(limits: new RestoreArchiveLimits { MaxExpandedBytes = 16 }).BuildPreviewAsync(
            CreateSettings(), Source(expanded, "B001_expanded.zip"), RestoreMode.Full);
        AssertRejected(expandedResult, "restore.archive_expanded_size_limit");

        var manifestArchive = CreateArchive("B001_manifest-limit.zip", [("B001_branch.bak", Bytes("bak"))], includeManifest: true);
        var manifestResult = await CreateService(limits: new RestoreArchiveLimits { MaxManifestBytes = 16 }).BuildPreviewAsync(
            CreateSettings(), Source(manifestArchive, "B001_manifest-limit.zip"), RestoreMode.Full);
        AssertRejected(manifestResult, "restore.archive_manifest_invalid");
    }

    [Fact]
    public async Task ArchiveEvidenceRejectsChecksumDuplicateMultipleBackupWrongBranchAndUnknownJson()
    {
        var checksum = CreateArchive(
            "B001_checksum.zip",
            [("B001_branch.bak", Bytes("checksum"))],
            includeManifest: true,
            checksumOverride: "0000000000000000000000000000000000000000000000000000000000000000");
        AssertRejected(
            await CreateService().BuildPreviewAsync(CreateSettings(), Source(checksum, "B001_checksum.zip"), RestoreMode.Full),
            "restore.archive_checksum_mismatch");

        var duplicate = CreateArchive(
            "B001_duplicate.zip",
            [("B001_branch.bak", Bytes("one")), ("b001_branch.bak", Bytes("two"))]);
        AssertRejected(
            await CreateService().BuildPreviewAsync(CreateSettings(), Source(duplicate, "B001_duplicate.zip"), RestoreMode.Full),
            "restore.archive_duplicate_entry");

        var multiple = CreateArchive(
            "B001_multiple.zip",
            [("B001_one.bak", Bytes("one")), ("B001_two.bak", Bytes("two"))]);
        AssertRejected(
            await CreateService().BuildPreviewAsync(CreateSettings(), Source(multiple, "B001_multiple.zip"), RestoreMode.Full),
            "restore.archive_bak_ambiguous");

        var wrongBranch = CreateArchive(
            "B001_wrong.zip",
            [("B002_branch.bak", Bytes("wrong"))],
            includeManifest: true,
            manifestBranchCode: "B002");
        AssertRejected(
            await CreateService().BuildPreviewAsync(CreateSettings(), Source(wrongBranch, "B001_wrong.zip"), RestoreMode.Full),
            "restore.archive_branch_mismatch");

        var legacyWrongBranch = CreateArchive(
            "B001_legacy-wrong.zip",
            [("B002_branch.bak", Bytes("wrong"))]);
        AssertRejected(
            await CreateService().BuildPreviewAsync(CreateSettings(), Source(legacyWrongBranch, "B001_legacy-wrong.zip"), RestoreMode.Full),
            "restore.archive_branch_mismatch");

        var unknownJson = CreateArchive(
            "B001_unknown.zip",
            [("B001_branch.bak", Bytes("bak")), ("secrets.json", Bytes("{\"password\":\"hidden\"}"))]);
        var unknownResult = await CreateService().BuildPreviewAsync(CreateSettings(), Source(unknownJson, "B001_unknown.zip"), RestoreMode.Full);
        AssertRejected(unknownResult, "restore.archive_unknown_json");
        Assert.DoesNotContain("hidden", unknownResult.SafeMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ManifestDestinationMappingAndConfiguredDestinationsAreServerOwned()
    {
        var archive = CreateArchive(
            "B001_mapping.zip",
            [
                ("B001_branch.bak", Bytes("bak")),
                ("branch-config.json", Bytes("{}")),
            ],
            includeManifest: true,
            componentOverrides: new Dictionary<string, string> { ["branch-config.json"] = "branch-database" });
        var mappingResult = await CreateService().BuildPreviewAsync(CreateSettings(), Source(archive, "B001_mapping.zip"), RestoreMode.Full);
        AssertRejected(mappingResult, "restore.archive_manifest_invalid");

        var unsafeSettings = CreateSettings();
        unsafeSettings.DbFilesPath = Path.Combine(_root, "db", "..", "outside");
        var safeArchive = CreateArchive("B001_destination.zip", [("B001_branch.bak", Bytes("bak"))]);
        var destinationResult = await CreateService().BuildPreviewAsync(unsafeSettings, Source(safeArchive, "B001_destination.zip"), RestoreMode.Full);
        AssertRejected(destinationResult, "restore.destination_unsafe");
    }

    [Fact]
    public async Task SqlLogicalFilesModesAndMovePlanRemainServerOwned()
    {
        var database = new FakeDatabaseService
        {
            RestoreFileList =
            [
                new RestoreFileInfo("data_one", "D"),
                new RestoreFileInfo("data_two", "D"),
                new RestoreFileInfo("log_one", "L"),
                new RestoreFileInfo("log_two", "L"),
            ],
        };
        var service = CreateService(database);
        var archive = CreateArchive("B001_moves.zip", [("B001_branch.bak", Bytes("bak"))]);
        var databaseOnly = await service.BuildPreviewAsync(CreateSettings(), Source(archive, "B001_moves.zip"), RestoreMode.DatabaseOnly);

        Assert.True(databaseOnly.Ready, databaseOnly.SafeMessage);
        Assert.Equal(
            ["RmsBranchSrv.mdf", "RmsBranchSrv_2.ndf", "RmsBranchSrv_log.ldf", "RmsBranchSrv_log_2.ldf"],
            databaseOnly.Intent!.SqlPlan.Moves.Select(move => move.DestinationFileName).ToArray());
        Assert.All(databaseOnly.Intent.SqlPlan.Moves, move => Assert.DoesNotContain(Path.GetTempPath(), move.DestinationFileName, StringComparison.OrdinalIgnoreCase));

        var configArchive = CreateArchive(
            "B001_config.zip",
            [("branch-config.json", Bytes("{\"mode\":\"config\"}"))],
            includeManifest: true);
        var configOnly = await service.BuildPreviewAsync(CreateSettings(), Source(configArchive, "B001_config.zip"), RestoreMode.ConfigOnly);
        Assert.True(configOnly.Ready, configOnly.SafeMessage);
        Assert.Empty(configOnly.Intent!.SqlPlan.Moves);
        Assert.Empty(database.RestoreCalls);
    }

    [Fact]
    public async Task EmptySqlInspectionFailsClosedWithoutExecutingRestore()
    {
        var database = new FakeDatabaseService { RestoreFileList = [] };
        var archive = CreateArchive("B001_empty-file-list.zip", [("B001_branch.bak", Bytes("bak"))]);
        var service = CreateService(database);

        var preview = await service.BuildPreviewAsync(
            CreateSettings(),
            Source(archive, "B001_empty-file-list.zip"),
            RestoreMode.DatabaseOnly);

        AssertRejected(preview, RestoreFailureCodes.SqlInspectionFailed);
        Assert.Empty(database.RestoreCalls);
        Assert.DoesNotContain("compatibility", preview.SafeMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestoreFailureAndPostCheckFailureAreSanitizedAndRestartServices()
    {
        var archive = CreateArchive("B001_failure.zip", [("B001_branch.bak", Bytes("bak"))]);
        var settings = CreateSettings();
        var database = new FakeDatabaseService { RestoreFailure = new InvalidOperationException($"secret path: {_root}\\private.bak") };
        var services = new FakeServiceManager();
        services.Statuses["TestService"] = ServiceStatus.Running;
        var service = CreateService(database, services);
        var preview = await service.BuildPreviewAsync(settings, Source(archive, "B001_failure.zip"), RestoreMode.DatabaseOnly);
        var failed = await service.ExecuteAsync(settings, Source(archive, "B001_failure.zip"), RestoreMode.DatabaseOnly, preview.Intent!.Fingerprint);

        Assert.Equal(OperationStatus.PartialSuccess, failed.Operation.Status);
        Assert.Equal(RestoreFailureCodes.DatabaseRestoreInterrupted, failed.FailureCode);
        Assert.Contains(RestoreFailureCodes.DatabaseRestoreInterruptedMessage, failed.Operation.Errors);
        Assert.DoesNotContain(_root, string.Join(' ', failed.Operation.Errors), StringComparison.OrdinalIgnoreCase);
        Assert.True(database.RestoreAttempted);
        Assert.False(database.RestoreCompleted);
        Assert.Contains(("TestService", ServiceControlAction.Start), services.Controls);

        database.RestoreFailure = null;
        database.RestoreVerificationResult = false;
        var verifyPreview = await service.BuildPreviewAsync(settings, Source(archive, "B001_failure.zip"), RestoreMode.DatabaseOnly);
        var verifyFailed = await service.ExecuteAsync(settings, Source(archive, "B001_failure.zip"), RestoreMode.DatabaseOnly, verifyPreview.Intent!.Fingerprint);
        Assert.Equal(OperationStatus.PartialSuccess, verifyFailed.Operation.Status);
        Assert.Equal("restore.verification_failed", verifyFailed.FailureCode);
    }

    [Fact]
    public async Task ConfigCopyFailureRollsBackAndCancellationStopsCleanly()
    {
        var settings = CreateSettings();
        var configArchive = CreateArchive(
            "B001_config-failure.zip",
            [("branch-config.json", Bytes("{\"replacement\":true}"))],
            includeManifest: true);
        var failOverwriteOnce = true;
        var failingFileSystem = new TestRestoreFileSystem(
            failCopy: (_, destination) =>
                string.Equals(Path.GetFullPath(destination), Path.GetFullPath(settings.BranchConfigPath), StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref failOverwriteOnce, false));
        var configService = CreateService(fileSystem: failingFileSystem);
        var configPreview = await configService.BuildPreviewAsync(settings, Source(configArchive, "B001_config-failure.zip"), RestoreMode.ConfigOnly);
        var configResult = await configService.ExecuteAsync(settings, Source(configArchive, "B001_config-failure.zip"), RestoreMode.ConfigOnly, configPreview.Intent!.Fingerprint);
        Assert.Equal(OperationStatus.Failed, configResult.Operation.Status);
        Assert.Equal("restore.config_copy_failed", configResult.FailureCode);
        Assert.Equal("{\"branch\":\"original\"}", await File.ReadAllTextAsync(settings.BranchConfigPath));

        var database = new FakeDatabaseService { BlockRestore = true };
        var services = new FakeServiceManager();
        services.Statuses["TestService"] = ServiceStatus.Running;
        var cancellationService = CreateService(database, services);
        var databaseArchive = CreateArchive("B001_cancel.zip", [("B001_branch.bak", Bytes("bak"))]);
        var cancelPreview = await cancellationService.BuildPreviewAsync(settings, Source(databaseArchive, "B001_cancel.zip"), RestoreMode.DatabaseOnly);
        using var cancellation = new CancellationTokenSource();
        var execution = cancellationService.ExecuteAsync(settings, Source(databaseArchive, "B001_cancel.zip"), RestoreMode.DatabaseOnly, cancelPreview.Intent!.Fingerprint, cancellation.Token);
        await database.RestoreStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var cancelled = await execution;
        Assert.Equal(OperationStatus.PartialSuccess, cancelled.Operation.Status);
        Assert.Equal(RestoreFailureCodes.DatabaseRestoreInterrupted, cancelled.FailureCode);
        Assert.Contains(RestoreFailureCodes.DatabaseRestoreInterruptedMessage, cancelled.Operation.Errors);
        Assert.True(database.RestoreAttempted);
        Assert.False(database.RestoreCompleted);
        Assert.Contains(("TestService", ServiceControlAction.Start), services.Controls);
        AssertNoStagingResidue(Path.Combine(_root, "staging"));
    }

    [Fact]
    public async Task FullRestoreDatabaseSuccessAndConfigurationFailureWithSuccessfulRollbackIsPartial()
    {
        var settings = CreateSettings();
        var archive = CreateArchive(
            "B001_full-config-failure.zip",
            [("B001_branch.bak", Bytes("bak")), ("branch-config.json", Bytes("{\"replacement\":true}"))],
            includeManifest: true);
        var failOverwriteOnce = true;
        var fileSystem = new TestRestoreFileSystem(
            failCopy: (_, destination) =>
                string.Equals(Path.GetFullPath(destination), Path.GetFullPath(settings.BranchConfigPath), StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref failOverwriteOnce, false));
        var database = new FakeDatabaseService();
        var services = new FakeServiceManager();
        services.Statuses["TestService"] = ServiceStatus.Running;
        var service = CreateService(database, services, fileSystem);

        var preview = await service.BuildPreviewAsync(settings, Source(archive, "B001_full-config-failure.zip"), RestoreMode.Full);
        var execution = await service.ExecuteAsync(settings, Source(archive, "B001_full-config-failure.zip"), RestoreMode.Full, preview.Intent!.Fingerprint);

        Assert.Equal(OperationStatus.PartialSuccess, execution.Operation.Status);
        Assert.Equal(RestoreFailureCodes.PartialFailure, execution.FailureCode);
        Assert.Single(database.RestoreCalls);
        Assert.Equal("{\"branch\":\"original\"}", await File.ReadAllTextAsync(settings.BranchConfigPath));
        Assert.Contains(execution.Operation.Messages, message => message.Contains("previous configuration was restored", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_root, string.Join(' ', execution.Operation.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfigurationRollbackFailureIsSanitizedAndRequiresRecovery()
    {
        var settings = CreateSettings();
        var archive = CreateArchive(
            "B001_rollback-failure.zip",
            [("branch-config.json", Bytes("{\"replacement\":true}"))],
            includeManifest: true);
        var fileSystem = new TestRestoreFileSystem(
            failCopy: (_, destination) =>
                string.Equals(Path.GetFullPath(destination), Path.GetFullPath(settings.BranchConfigPath), StringComparison.OrdinalIgnoreCase));
        var service = CreateService(fileSystem: fileSystem);

        var preview = await service.BuildPreviewAsync(settings, Source(archive, "B001_rollback-failure.zip"), RestoreMode.ConfigOnly);
        var execution = await service.ExecuteAsync(settings, Source(archive, "B001_rollback-failure.zip"), RestoreMode.ConfigOnly, preview.Intent!.Fingerprint);

        Assert.Equal(OperationStatus.PartialSuccess, execution.Operation.Status);
        Assert.Equal(RestoreFailureCodes.ConfigRollbackFailed, execution.FailureCode);
        Assert.Contains(execution.Operation.Messages, message => message.Contains("manual recovery", StringComparison.OrdinalIgnoreCase));
        var evidence = string.Join(' ', execution.Operation.Errors.Concat(execution.Operation.Messages));
        Assert.DoesNotContain(_root, evidence, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IOException", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServiceRestartFailureIsPartialAndAllRestartAttemptsContinue()
    {
        var settings = CreateSettings();
        settings.Services = ["TestService", "SecondService"];
        var archive = CreateArchive("B001_restart-failure.zip", [("B001_branch.bak", Bytes("bak"))]);
        var database = new FakeDatabaseService();
        var services = new FakeServiceManager();
        services.Statuses["TestService"] = ServiceStatus.Running;
        services.Statuses["SecondService"] = ServiceStatus.Running;
        services.StartFailures.Add("SecondService");
        var service = CreateService(database, services);

        var preview = await service.BuildPreviewAsync(settings, Source(archive, "B001_restart-failure.zip"), RestoreMode.DatabaseOnly);
        var execution = await service.ExecuteAsync(settings, Source(archive, "B001_restart-failure.zip"), RestoreMode.DatabaseOnly, preview.Intent!.Fingerprint);

        Assert.Equal(OperationStatus.PartialSuccess, execution.Operation.Status);
        Assert.Equal(RestoreFailureCodes.ServiceRestartFailed, execution.FailureCode);
        Assert.Single(database.RestoreCalls);
        Assert.Contains(("TestService", ServiceControlAction.Start), services.Controls);
        Assert.Contains(("SecondService", ServiceControlAction.Start), services.Controls);
    }

    [Fact]
    public async Task CancellationBeforeDestructiveWorkRemainsCancelled()
    {
        var settings = CreateSettings();
        var archive = CreateArchive("B001_cancel-before-work.zip", [("B001_branch.bak", Bytes("bak"))]);
        var database = new FakeDatabaseService();
        var services = new FakeServiceManager();
        services.Statuses["TestService"] = ServiceStatus.Running;
        var service = CreateService(database, services);
        var preview = await service.BuildPreviewAsync(settings, Source(archive, "B001_cancel-before-work.zip"), RestoreMode.DatabaseOnly);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var execution = await service.ExecuteAsync(
            settings,
            Source(archive, "B001_cancel-before-work.zip"),
            RestoreMode.DatabaseOnly,
            preview.Intent!.Fingerprint,
            cancellation.Token);

        Assert.Equal(OperationStatus.Cancelled, execution.Operation.Status);
        Assert.Equal("restore.cancelled", execution.FailureCode);
        Assert.False(database.RestoreAttempted);
        Assert.False(database.RestoreCompleted);
        Assert.Empty(database.RestoreCalls);
        Assert.Empty(services.Controls);
    }

    [Fact]
    public async Task CancellationAfterDatabaseRestoreIsPartialAndDoesNotClaimReversal()
    {
        var settings = CreateSettings();
        var archive = CreateArchive(
            "B001_cancel-after-database.zip",
            [("B001_branch.bak", Bytes("bak")), ("branch-config.json", Bytes("{\"replacement\":true}"))],
            includeManifest: true);
        using var cancellation = new CancellationTokenSource();
        var database = new FakeDatabaseService { AfterRestore = cancellation.Cancel };
        var services = new FakeServiceManager();
        services.Statuses["TestService"] = ServiceStatus.Running;
        var service = CreateService(database, services);

        var preview = await service.BuildPreviewAsync(settings, Source(archive, "B001_cancel-after-database.zip"), RestoreMode.Full);
        var execution = await service.ExecuteAsync(
            settings,
            Source(archive, "B001_cancel-after-database.zip"),
            RestoreMode.Full,
            preview.Intent!.Fingerprint,
            cancellation.Token);

        Assert.Equal(OperationStatus.PartialSuccess, execution.Operation.Status);
        Assert.Equal(RestoreFailureCodes.CancelledAfterDestructiveWork, execution.FailureCode);
        Assert.True(database.RestoreAttempted);
        Assert.True(database.RestoreCompleted);
        Assert.Single(database.RestoreCalls);
        Assert.Equal("{\"branch\":\"original\"}", await File.ReadAllTextAsync(settings.BranchConfigPath));
        Assert.Contains(execution.Operation.Messages, message => message.Contains("did not reverse", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(execution.Operation.Messages, message => message.Contains("rolled back", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FullRestoreInterruptedDuringDatabaseDoesNotStartConfigurationOverwrite()
    {
        var settings = CreateSettings();
        var archive = CreateArchive(
            "B001_full-interrupted-database.zip",
            [
                ("B001_branch.bak", Bytes("bak")),
                ("branch-config.json", Bytes("{\"replacement\":true}")),
            ],
            includeManifest: true);
        var database = new FakeDatabaseService { BlockRestore = true };
        var services = new FakeServiceManager();
        services.Statuses["TestService"] = ServiceStatus.Running;
        var service = CreateService(database, services);
        var preview = await service.BuildPreviewAsync(settings, Source(archive, "B001_full-interrupted-database.zip"), RestoreMode.Full);
        using var cancellation = new CancellationTokenSource();
        var execution = service.ExecuteAsync(
            settings,
            Source(archive, "B001_full-interrupted-database.zip"),
            RestoreMode.Full,
            preview.Intent!.Fingerprint,
            cancellation.Token);

        await database.RestoreStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var interrupted = await execution;

        Assert.Equal(OperationStatus.PartialSuccess, interrupted.Operation.Status);
        Assert.Equal(RestoreFailureCodes.DatabaseRestoreInterrupted, interrupted.FailureCode);
        Assert.True(database.RestoreAttempted);
        Assert.False(database.RestoreCompleted);
        Assert.Empty(database.RestoreCalls);
        Assert.Equal("{\"branch\":\"original\"}", await File.ReadAllTextAsync(settings.BranchConfigPath));
        Assert.Contains(("TestService", ServiceControlAction.Start), services.Controls);
        Assert.Contains(interrupted.Operation.Messages, message => message.Contains("must be verified", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private RestoreService CreateService(
        FakeDatabaseService? database = null,
        FakeServiceManager? services = null,
        TestRestoreFileSystem? fileSystem = null,
        RestoreArchiveLimits? limits = null,
        string? stagingRoot = null) =>
        new(
            database ?? new FakeDatabaseService(),
            fileSystem ?? new TestRestoreFileSystem(),
            services,
            new RestoreSqlPlanBuilder(),
            limits ?? new RestoreArchiveLimits(),
            TimeProvider.System,
            stagingRoot ?? Path.Combine(_root, "staging"));

    private AppSettings CreateSettings()
    {
        var dbPath = Path.Combine(_root, "db-files");
        var configPath = Path.Combine(_root, "config");
        Directory.CreateDirectory(dbPath);
        Directory.CreateDirectory(configPath);
        var branch = Path.Combine(configPath, "branch.json");
        var cashierServer = Path.Combine(configPath, "cashier-server.json");
        var cashierUi = Path.Combine(configPath, "cashier-ui.json");
        File.WriteAllText(branch, "{\"branch\":\"original\"}");
        File.WriteAllText(cashierServer, "{}\n");
        File.WriteAllText(cashierUi, "{}\n");
        return new AppSettings
        {
            BranchCode = "B001",
            Databases = ["RmsBranchSrv"],
            DbFilesPath = dbPath,
            BranchConfigPath = branch,
            CashierGrpcConfigPath = cashierServer,
            CashierUiConfigPath = cashierUi,
            Services = ["TestService"],
        };
    }

    private static RestoreSourceDescriptor Source(string path, string displayName) =>
        new(
            new RestoreSourceReference(RestoreSourceKind.BrowseHandle, "test-root", displayName, null, displayName),
            path);

    private string CreateArchive(
        string fileName,
        IReadOnlyList<(string Name, byte[] Content)> contents,
        bool includeManifest = false,
        string? manifestBranchCode = null,
        string? checksumOverride = null,
        CompressionLevel compression = CompressionLevel.NoCompression,
        IReadOnlyDictionary<string, string>? componentOverrides = null)
    {
        var path = Path.Combine(_root, fileName);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        var manifest = new List<object>();
        foreach (var content in contents)
        {
            var entry = archive.CreateEntry(content.Name, compression);
            using (var output = entry.Open()) output.Write(content.Content);
            if (includeManifest)
            {
                var component = componentOverrides is not null && componentOverrides.TryGetValue(content.Name, out var overrideValue)
                    ? overrideValue
                    : content.Name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ? "branch-database" : "branch-config";
                manifest.Add(new
                {
                    schemaVersion = 1,
                    archiveName = content.Name,
                    displayName = content.Name,
                    sizeBytes = content.Content.LongLength,
                    sha256Checksum = checksumOverride ?? Convert.ToHexString(SHA256.HashData(content.Content)).ToLowerInvariant(),
                    componentId = component,
                });
            }
        }

        if (includeManifest)
        {
            var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                branchCode = manifestBranchCode ?? "B001",
                posNumber = "07",
                contents = manifest,
            });
            var entry = archive.CreateEntry("manifest.json", compression);
            using var output = entry.Open();
            output.Write(manifestBytes);
        }

        return path;
    }

    private string CreateSymlinkArchive(string fileName)
    {
        var path = Path.Combine(_root, fileName);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        var entry = archive.CreateEntry("B001_branch.bak", CompressionLevel.NoCompression);
        entry.ExternalAttributes = unchecked((int)0xA0000000);
        using var output = entry.Open();
        output.Write(Bytes("link"));
        return path;
    }

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    private static void AssertRejected(RestorePreviewBuildResult result, string code)
    {
        Assert.False(result.Ready);
        Assert.Equal(code, result.ErrorCode);
        Assert.Null(result.Intent);
    }

    private static void AssertNoStagingResidue(string stagingRoot)
    {
        if (Directory.Exists(stagingRoot)) Assert.Empty(Directory.EnumerateFileSystemEntries(stagingRoot));
    }

    private sealed class FakeDatabaseService : IDatabaseService, IDatabaseRestoreVerifier
    {
        public IReadOnlyList<RestoreFileInfo> RestoreFileList { get; init; } =
            [new RestoreFileInfo("branch_data", "D"), new RestoreFileInfo("branch_log", "L")];

        public List<(string DatabaseName, string BackupPath, IReadOnlyList<RestoreFileInfo> LogicalFiles, string DbFilesPath)> RestoreCalls { get; } = [];

        public Exception? RestoreFailure { get; set; }

        public bool RestoreVerificationResult { get; set; } = true;

        public bool BlockRestore { get; set; }

        public bool RestoreAttempted { get; private set; }

        public bool RestoreCompleted { get; private set; }

        public Action? AfterRestoreAttempt { get; set; }

        public Action? AfterRestore { get; set; }

        public TaskCompletionSource RestoreStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task TestConnectionAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> BranchExistsAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task ResetBranchDataAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task BackupDatabaseAsync(AppSettings settings, string databaseName, string backupFilePath, bool useCompatibilityMode, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<RestoreFileInfo>> ReadRestoreFileListAsync(AppSettings settings, string backupFilePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(RestoreFileList);

        public async Task RestoreDatabaseAsync(
            AppSettings settings,
            string targetDatabase,
            string backupFilePath,
            IReadOnlyList<RestoreFileInfo> logicalFiles,
            string dbFilesPath,
            CancellationToken cancellationToken = default)
        {
            RestoreAttempted = true;
            RestoreStarted.TrySetResult();
            AfterRestoreAttempt?.Invoke();
            if (BlockRestore) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (RestoreFailure is not null) throw RestoreFailure;
            RestoreCalls.Add((targetDatabase, backupFilePath, logicalFiles, dbFilesPath));
            AfterRestore?.Invoke();
            RestoreCompleted = true;
        }

        public Task<bool> VerifyRestoreAsync(AppSettings settings, string targetDatabase, CancellationToken cancellationToken = default) =>
            Task.FromResult(RestoreVerificationResult);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeServiceManager : IServiceManager
    {
        public Dictionary<string, ServiceStatus> Statuses { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<(string Service, ServiceControlAction Action)> Controls { get; } = [];

        public HashSet<string> StartFailures { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<ServiceStatus> GetStatusAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Statuses.GetValueOrDefault(serviceName, ServiceStatus.NotFound));

        public Task<IReadOnlyDictionary<string, ServiceStatus>> GetStatusesAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, ServiceStatus>>(serviceNames.ToDictionary(name => name, name => Statuses.GetValueOrDefault(name, ServiceStatus.NotFound), StringComparer.OrdinalIgnoreCase));

        public Task ControlAsync(string serviceName, ServiceControlAction action, CancellationToken cancellationToken = default)
        {
            Controls.Add((serviceName, action));
            if (action == ServiceControlAction.Start && StartFailures.Contains(serviceName))
            {
                return Task.FromException(new InvalidOperationException("test restart failure"));
            }

            Statuses[serviceName] = action == ServiceControlAction.Stop ? ServiceStatus.Stopped : ServiceStatus.Running;
            return Task.CompletedTask;
        }
    }

    private sealed class TestRestoreFileSystem : IRestoreFileSystem
    {
        private readonly IRestoreFileSystem _inner = new RestoreFileSystem();
        private readonly Func<string, bool>? _reparse;
        private readonly Func<string, bool>? _failCopyDestination;
        private readonly Func<string, string, bool>? _failCopy;

        public TestRestoreFileSystem(
            Func<string, bool>? reparse = null,
            Func<string, bool>? failCopyDestination = null,
            Func<string, string, bool>? failCopy = null)
        {
            _reparse = reparse;
            _failCopyDestination = failCopyDestination;
            _failCopy = failCopy;
        }

        public bool FileExists(string path) => _inner.FileExists(path);
        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
        public bool IsReparsePoint(string path) => _reparse?.Invoke(path) == true || _inner.IsReparsePoint(path);
        public IReadOnlyList<string> EnumerateFileSystemEntries(string directoryPath) => _inner.EnumerateFileSystemEntries(directoryPath);
        public long GetFileLength(string path) => _inner.GetFileLength(path);
        public DateTimeOffset GetLastWriteTimeUtc(string path) => _inner.GetLastWriteTimeUtc(path);
        public long GetAvailableFreeSpace(string path) => _inner.GetAvailableFreeSpace(path);
        public string GetFullPath(string path) => _inner.GetFullPath(path);
        public Task EnsureDirectoryAsync(string path, CancellationToken cancellationToken = default) => _inner.EnsureDirectoryAsync(path, cancellationToken);
        public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default) => _inner.OpenReadAsync(path, cancellationToken);
        public Task<Stream> CreateFileAsync(string path, CancellationToken cancellationToken = default) => _inner.CreateFileAsync(path, cancellationToken);

        public Task CopyFileAtomicAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        {
            if (_failCopyDestination?.Invoke(destinationPath) == true
                || _failCopy?.Invoke(sourcePath, destinationPath) == true)
            {
                return Task.FromException(new IOException("test copy failure"));
            }

            return _inner.CopyFileAtomicAsync(sourcePath, destinationPath, cancellationToken);
        }

        public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default) => _inner.DeleteFileAsync(path, cancellationToken);
        public Task DeleteDirectoryAsync(string path, CancellationToken cancellationToken = default) => _inner.DeleteDirectoryAsync(path, cancellationToken);
        public Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default) => _inner.ComputeSha256Async(path, cancellationToken);
    }
}
