using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Exceptions;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

public sealed class DbDownloadServiceTests
{
    [Fact]
    public async Task RunAsyncPicksMostRecentlyCreatedFolderNotHighestSerial()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new FakeBackupRepository();
        repository.Directories.Add(new RemoteEntryInfo("99999999", @"D:\DbBackups\99999999", now, 0));
        repository.Directories.Add(new RemoteEntryInfo("111", @"D:\DbBackups\111", now.AddSeconds(2), 0));
        repository.FilesByFolder[@"D:\DbBackups\111"] =
        [
            new RemoteEntryInfo("P087_111.zip", @"D:\DbBackups\111\P087_111.zip", now, 1024)
        ];

        var service = new DbDownloadService(new FakeApiClient(), repository);
        var settings = new DbDownloaderSettings { BackupRootFolder = @"D:\DbBackups", PollIntervalSeconds = 1, TimeoutSeconds = 10 };

        var job = await service.RunAsync(settings, ["P087"]);

        Assert.Equal(@"D:\DbBackups\111", job.BatchFolderPath);
        Assert.Equal(BranchBackupStatus.Ready, job.Items.Single().Status);
    }

    [Fact]
    public async Task RunAsyncIgnoresChunkFilesAndOnlyMatchesExactZipName()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new FakeBackupRepository();
        repository.Directories.Add(new RemoteEntryInfo("40760799", @"D:\DbBackups\40760799", now, 0));
        repository.FilesByFolder[@"D:\DbBackups\40760799"] =
        [
            new RemoteEntryInfo("chunk1.tmp", @"D:\DbBackups\40760799\chunk1.tmp", now, 512),
            new RemoteEntryInfo("P087.partial", @"D:\DbBackups\40760799\P087.partial", now, 512),
            new RemoteEntryInfo("P087_40760799.zip", @"D:\DbBackups\40760799\P087_40760799.zip", now, 2048)
        ];

        var service = new DbDownloadService(new FakeApiClient(), repository);
        var settings = new DbDownloaderSettings { BackupRootFolder = @"D:\DbBackups", PollIntervalSeconds = 1, TimeoutSeconds = 10 };

        var job = await service.RunAsync(settings, ["P087"]);
        var item = job.Items.Single();

        Assert.Equal(BranchBackupStatus.Ready, item.Status);
        Assert.Equal(@"D:\DbBackups\40760799\P087_40760799.zip", item.RemoteZipPath);
        Assert.Equal("40760799", job.Serial);
    }

    [Fact]
    public async Task RunAsyncTracksBranchesIndependentlyWithinABatch()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new FakeBackupRepository();
        repository.Directories.Add(new RemoteEntryInfo("555", @"D:\DbBackups\555", now, 0));
        repository.FilesByFolder[@"D:\DbBackups\555"] =
        [
            new RemoteEntryInfo("P087_555.zip", @"D:\DbBackups\555\P087_555.zip", now, 4096)
        ];

        var service = new DbDownloadService(new FakeApiClient(), repository);
        var settings = new DbDownloaderSettings { BackupRootFolder = @"D:\DbBackups", PollIntervalSeconds = 1, TimeoutSeconds = 1 };

        var job = await service.RunAsync(settings, ["P087", "P091"]);

        var ready = job.Items.Single(i => i.BranchCode == "P087");
        var timedOut = job.Items.Single(i => i.BranchCode == "P091");

        Assert.Equal(BranchBackupStatus.Ready, ready.Status);
        Assert.Equal(BranchBackupStatus.TimedOut, timedOut.Status);
    }

    [Fact]
    public async Task RunAsyncTimesOutWhenBatchFolderNeverAppears()
    {
        var repository = new FakeBackupRepository();
        var service = new DbDownloadService(new FakeApiClient(), repository);
        var settings = new DbDownloaderSettings { BackupRootFolder = @"D:\DbBackups", PollIntervalSeconds = 1, TimeoutSeconds = 1 };

        var job = await service.RunAsync(settings, ["P087"]);

        Assert.All(job.Items, item => Assert.Equal(BranchBackupStatus.TimedOut, item.Status));
    }

    [Fact]
    public async Task RunWithOutcomeAsync_PreservesAcceptedTriggerWhenRepositoryFails()
    {
        var repository = new FakeBackupRepository
        {
            ListDirectoriesException = new BackupRepositoryException(DownloaderFailureCodes.SmbConnectionFailed)
        };
        var service = new DbDownloadService(new FakeApiClient(), repository);
        var settings = new DbDownloaderSettings { BackupRootFolder = @"D:\DbBackups", TimeoutSeconds = 10 };

        var execution = await service.RunWithOutcomeAsync(settings, ["P087", "P091"]);

        Assert.True(execution.TriggerAccepted);
        Assert.Equal(DownloaderFailureCodes.SmbConnectionFailed, execution.FailureCode);
        Assert.All(execution.Job.Items, item =>
        {
            Assert.Equal(BranchBackupStatus.Failed, item.Status);
            Assert.Equal(DownloaderFailureCodes.SmbConnectionFailed, item.FailureCode);
        });
    }

    [Fact]
    public async Task RunWithOutcomeAsync_UnknownTriggerStopsBeforeSmbAndPreservesGuidance()
    {
        var repository = new FakeBackupRepository();
        var api = new FakeApiClient
        {
            Result = new DownloaderTriggerResult(
                DownloaderTriggerState.OutcomeUnknown,
                DownloaderFailureCodes.TriggerOutcomeUnknown)
        };
        var service = new DbDownloadService(api, repository);

        var execution = await service.RunWithOutcomeAsync(
            new DbDownloaderSettings { BackupRootFolder = @"D:\DbBackups", TimeoutSeconds = 10 },
            ["P087"]);

        Assert.Equal(DownloaderTriggerState.OutcomeUnknown, execution.TriggerState);
        Assert.Equal(DownloaderFailureCodes.TriggerOutcomeUnknown, execution.FailureCode);
        Assert.Equal(BranchBackupStatus.Failed, execution.Job.Items.Single().Status);
        Assert.Equal(DownloaderFailureCodes.TriggerOutcomeUnknown, execution.Job.Items.Single().FailureCode);
        Assert.Contains("before retrying", execution.Job.Items.Single().ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repository.ListDirectoriesCalls);
    }

    [Fact]
    public async Task DownloadAsyncMarksItemDownloadedAndUsesRepository()
    {
        var repository = new FakeBackupRepository();
        var service = new DbDownloadService(new FakeApiClient(), repository);
        var settings = new DbDownloaderSettings { RdbServerIp = "10.0.0.1", RdbUsername = "svc", RdbPassword = "pw" };
        var item = new BranchBackupItem("P087") { RemoteZipPath = @"D:\DbBackups\555\P087_555.zip" };

        await service.DownloadAsync(settings, item, @"C:\Downloads");

        Assert.Equal(BranchBackupStatus.Downloaded, item.Status);
        Assert.Single(repository.Downloaded);
        Assert.Equal(@"D:\DbBackups\555\P087_555.zip", repository.Downloaded[0].RemotePath);
    }

    private sealed class FakeApiClient : IBackupApiClient
    {
        public DownloaderTriggerResult Result { get; init; } =
            new(DownloaderTriggerState.Accepted);

        public Task<DownloaderTriggerResult> TriggerBackupAsync(
            string apiUrl,
            IReadOnlyList<string> branchCodes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result);
    }

    private sealed class FakeBackupRepository : IBackupRepository
    {
        public Exception? ListDirectoriesException { get; init; }

        public Exception? ListFilesException { get; init; }

        public List<RemoteEntryInfo> Directories { get; } = [];

        public Dictionary<string, List<RemoteEntryInfo>> FilesByFolder { get; } = [];

        public List<(string RemotePath, string LocalPath)> Downloaded { get; } = [];

        public int ListDirectoriesCalls { get; private set; }

        public Task<IReadOnlyList<RemoteEntryInfo>> ListDirectoriesAsync(RemoteConnectionInfo connection, string rootFolder, CancellationToken cancellationToken = default)
        {
            ListDirectoriesCalls++;
            if (ListDirectoriesException is not null) throw ListDirectoriesException;
            return Task.FromResult((IReadOnlyList<RemoteEntryInfo>)Directories);
        }

        public Task<IReadOnlyList<RemoteEntryInfo>> ListFilesAsync(RemoteConnectionInfo connection, string folder, CancellationToken cancellationToken = default)
        {
            if (ListFilesException is not null) throw ListFilesException;
            var files = FilesByFolder.TryGetValue(folder, out var value) ? value : [];
            return Task.FromResult((IReadOnlyList<RemoteEntryInfo>)files);
        }

        public Task DownloadFileAsync(RemoteConnectionInfo connection, string remoteFilePath, string localFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            Downloaded.Add((remoteFilePath, localFilePath));
            return Task.CompletedTask;
        }
    }
}
