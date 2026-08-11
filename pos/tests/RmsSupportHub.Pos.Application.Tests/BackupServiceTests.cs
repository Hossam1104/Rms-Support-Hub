using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pos-admin-backup-tests-").FullName;

    [Fact]
    public async Task ExecuteAsync_WithAllComponents_CreatesManifestAndCleansStaging()
    {
        var destination = CreateDirectory("destination");
        var settings = CreateSettings();
        CreateConfigurationSources(settings);
        var database = new FakeDatabaseService();
        var service = new BackupService(database, new TestBackupFileSystem());

        var result = await service.ExecuteAsync(
            settings,
            ["branch-database", "cashier-database", "branch-config", "cashier-server-config", "cashier-ui-config"],
            destination);

        Assert.Equal(OperationStatus.Success, result.Operation.Status);
        Assert.NotNull(result.Artifact);
        Assert.Equal(2, database.Calls.Count);
        Assert.All(database.Calls, call => Assert.False(call.UseCompatibilityMode));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(destination), path => Path.GetFileName(path).StartsWith(".pos_backup_", StringComparison.Ordinal));

        using var archive = ZipFile.OpenRead(result.Artifact!.ArchivePath);
        Assert.Contains(archive.Entries, entry => entry.FullName == "manifest.json");
        Assert.Contains(archive.Entries, entry => entry.FullName == "RMS_BranchService_appsettings.json");
        Assert.Contains(archive.Entries, entry => entry.FullName == "RMS_CashierServer_appsettings.json");
        Assert.Contains(archive.Entries, entry => entry.FullName == "RMS_CashierUI_appsettings.json");

        var manifestEntry = archive.GetEntry("manifest.json");
        Assert.NotNull(manifestEntry);
        using var manifestStream = manifestEntry!.Open();
        using var document = JsonDocument.Parse(manifestStream);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("B001", document.RootElement.GetProperty("branchCode").GetString());
        Assert.Equal(5, document.RootElement.GetProperty("contents").GetArrayLength());
        Assert.All(
            document.RootElement.GetProperty("contents").EnumerateArray(),
            item => Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("sha256Checksum").GetString())));
    }

    [Fact]
    public async Task ExecuteAsync_WhenPrimaryDatabaseBackupFails_RetriesCompatibilityMode()
    {
        var destination = CreateDirectory("retry-destination");
        var settings = CreateSettings();
        var database = new FakeDatabaseService { FailPrimaryAttempt = true };
        var service = new BackupService(database, new TestBackupFileSystem());

        var result = await service.ExecuteAsync(settings, ["branch-database"], destination);

        Assert.Equal(OperationStatus.Success, result.Operation.Status);
        Assert.Equal(
            [("RmsBranchSrv", false), ("RmsBranchSrv", true)],
            database.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOneDatabaseCannotBeBackedUp_ReturnsPartialArchive()
    {
        var destination = CreateDirectory("partial-destination");
        var settings = CreateSettings();
        CreateConfigurationSources(settings);
        var database = new FakeDatabaseService { FailureDatabaseName = "RmsBranchSrv" };
        var service = new BackupService(database, new TestBackupFileSystem());

        var result = await service.ExecuteAsync(settings, ["branch-database", "branch-config"], destination);

        Assert.Equal(OperationStatus.PartialSuccess, result.Operation.Status);
        Assert.NotNull(result.Artifact);
        Assert.Contains(result.Operation.Errors, error => error.Contains("RmsBranchSrv database", StringComparison.Ordinal));
        using var archive = ZipFile.OpenRead(result.Artifact!.ArchivePath);
        Assert.Contains(archive.Entries, entry => entry.FullName == "RMS_BranchService_appsettings.json");
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("RmsBranchSrv_", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenSourceIsMissingOrDestinationIsFull_ReportsSafePreflightErrors()
    {
        var destination = CreateDirectory("preflight-destination");
        var settings = CreateSettings();
        settings.BranchConfigPath = Path.Combine(_root, "missing-appsettings.json");
        var fileSystem = new TestBackupFileSystem { AvailableFreeSpaceBytes = 0 };
        var service = new BackupService(new FakeDatabaseService(), fileSystem);

        var validation = service.Validate(settings, ["branch-config", "branch-database"], destination);

        Assert.False(validation.Ready);
        Assert.Contains(validation.Errors, error => error.Code == BackupValidationErrorCodes.ConfigurationSourceMissing);
        Assert.Contains(validation.Errors, error => error.Code == BackupValidationErrorCodes.InsufficientSpace);
        Assert.DoesNotContain(validation.Errors, error => error.Message.Contains(_root, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WhenDatabaseCancels_CleansStagingAndProducesNoArchive()
    {
        var destination = CreateDirectory("cancel-destination");
        var settings = CreateSettings();
        var database = new FakeDatabaseService { CancelImmediately = true };
        var service = new BackupService(database, new TestBackupFileSystem());

        var result = await service.ExecuteAsync(settings, ["branch-database"], destination);

        Assert.Equal(OperationStatus.Cancelled, result.Operation.Status);
        Assert.Null(result.Artifact);
        Assert.Empty(Directory.EnumerateFiles(destination, "*.zip"));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(destination), path => Path.GetFileName(path).StartsWith(".pos_backup_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationArrivesAfterArchiveMove_RemovesTheUnpublishedArchive()
    {
        var destination = CreateDirectory("post-move-cancel-destination");
        var settings = CreateSettings();
        var cancellation = new CancellationTokenSource();
        var fileSystem = new TestBackupFileSystem
        {
            BeforeComputeHash = path =>
            {
                if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase)) cancellation.Cancel();
            },
        };
        var service = new BackupService(new FakeDatabaseService(), fileSystem);

        var result = await service.ExecuteAsync(settings, ["branch-database"], destination, cancellationToken: cancellation.Token);

        Assert.Equal(OperationStatus.Cancelled, result.Operation.Status);
        Assert.Null(result.Artifact);
        Assert.Empty(Directory.EnumerateFiles(destination, "*.zip"));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(destination), path => Path.GetFileName(path).StartsWith(".pos_backup_", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private void CreateConfigurationSources(AppSettings settings)
    {
        settings.BranchConfigPath = CreateSource("branch.json", "branch-config");
        settings.CashierGrpcConfigPath = CreateSource("cashier-server.json", "cashier-server-config");
        settings.CashierUiConfigPath = CreateSource("cashier-ui.json", "cashier-ui-config");
    }

    private string CreateSource(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static AppSettings CreateSettings() => new()
    {
        ClientName = "Test Client",
        BranchCode = "B001",
        PosNumber = "07",
        Release = "test-release",
        Databases = ["RmsCashierSrv", "RmsBranchSrv"],
    };

    private sealed class FakeDatabaseService : IDatabaseService
    {
        public List<(string DatabaseName, bool UseCompatibilityMode)> Calls { get; } = [];

        public string? FailureDatabaseName { get; init; }

        public bool FailPrimaryAttempt { get; init; }

        public bool CancelImmediately { get; init; }

        public Task TestConnectionAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> BranchExistsAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task ResetBranchDataAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task BackupDatabaseAsync(AppSettings settings, string databaseName, string backupFilePath, bool useCompatibilityMode, CancellationToken cancellationToken = default)
        {
            Calls.Add((databaseName, useCompatibilityMode));
            cancellationToken.ThrowIfCancellationRequested();
            if (CancelImmediately) throw new OperationCanceledException(cancellationToken);
            if (string.Equals(FailureDatabaseName, databaseName, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("database failure");
            if (FailPrimaryAttempt && !useCompatibilityMode) throw new InvalidOperationException("primary backup failure");
            File.WriteAllText(backupFilePath, $"backup:{databaseName}:{useCompatibilityMode}");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RestoreFileInfo>> ReadRestoreFileListAsync(AppSettings settings, string backupFilePath, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RestoreFileInfo>>([]);

        public Task RestoreDatabaseAsync(AppSettings settings, string targetDatabase, string backupFilePath, IReadOnlyList<RestoreFileInfo> logicalFiles, string dbFilesPath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestBackupFileSystem : IBackupFileSystem
    {
        public long AvailableFreeSpaceBytes { get; init; } = long.MaxValue;

        public Action<string>? BeforeComputeHash { get; init; }

        public BackupDestinationInfo InspectDestination(string path) =>
            new(Directory.Exists(path), Directory.Exists(path), AvailableFreeSpaceBytes);

        public bool FileExists(string path) => File.Exists(path);

        public bool IsReparsePoint(string path) =>
            File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);

        public long GetFileLength(string path) => new FileInfo(path).Length;

        public Task EnsureDirectoryAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(path);
            return Task.CompletedTask;
        }

        public async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var source = File.OpenRead(sourcePath);
            await using var destination = File.Create(destinationPath);
            await source.CopyToAsync(destination, cancellationToken);
        }

        public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream>(File.OpenRead(path));
        }

        public Task<Stream> CreateFileAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream>(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None));
        }

        public Task MoveFileAsync(string sourcePath, string destinationPath, bool overwrite, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(sourcePath, destinationPath, overwrite);
            return Task.CompletedTask;
        }

        public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        public Task DeleteDirectoryAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            return Task.CompletedTask;
        }

        public async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
        {
            BeforeComputeHash?.Invoke(path);
            await using var stream = File.OpenRead(path);
            return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
        }
    }
}
