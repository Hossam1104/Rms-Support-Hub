using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

public sealed class DownloaderBackendTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StableSizeObservation_RequiresASecondEqualPositiveObservation()
    {
        var clock = new ManualClock(Start);
        var delay = new AdvancingDelay(clock);
        var repository = new FakeBackupRepository
        {
            Directories = [new("batch", @"D:\DbBackups\batch", Start, 0)]
        };
        repository.FileSequences[@"D:\DbBackups\batch"] =
        [
            [new("B01_1.zip", @"D:\DbBackups\batch\B01_1.zip", Start, 10)],
            [new("B01_1.zip", @"D:\DbBackups\batch\B01_1.zip", Start, 20)],
            [new("B01_1.zip", @"D:\DbBackups\batch\B01_1.zip", Start, 20)]
        ];

        var service = new DbDownloadService(new FakeApiClient(), repository, clock, delay);
        var job = await service.RunAsync(
            new DbDownloaderSettings
            {
                BackupRootFolder = @"D:\DbBackups",
                TimeoutSeconds = 30,
                PollIntervalSeconds = 1,
                StableSizeObservationAttempts = 3,
                StableSizeObservationIntervalSeconds = 2
            },
            ["B01"]);

        Assert.Equal(BranchBackupStatus.Ready, job.Items.Single().Status);
        Assert.Equal(20, job.Items.Single().LastObservedSizeBytes);
        Assert.Equal(TimeSpan.FromSeconds(4), delay.Total);
    }

    [Fact]
    public async Task CancellationDuringPolling_IsImmediateAndMarksOnlyOpenBranchesCancelled()
    {
        var clock = new ManualClock(Start);
        using var cancellation = new CancellationTokenSource();
        var delay = new AdvancingDelay(clock, () => cancellation.Cancel());
        var repository = new FakeBackupRepository
        {
            Directories = []
        };
        var service = new DbDownloadService(new FakeApiClient(), repository, clock, delay);

        var job = await service.RunAsync(
            new DbDownloaderSettings { BackupRootFolder = @"D:\DbBackups", TimeoutSeconds = 30 },
            ["B01", "B02"],
            cancellationToken: cancellation.Token);

        Assert.All(job.Items, item => Assert.Equal(BranchBackupStatus.Cancelled, item.Status));
        Assert.All(job.Items, item => Assert.Equal(DownloaderFailureCodes.DownloadCancelled, item.FailureCode));
        Assert.Single(delay.Delays);
    }

    [Fact]
    public async Task ExactBranchMatching_DoesNotConsumeAlongBranchArchive()
    {
        var clock = new ManualClock(Start);
        var repository = new FakeBackupRepository
        {
            Directories = [new("batch", @"D:\DbBackups\batch", Start, 0)]
        };
        repository.FileSequences[@"D:\DbBackups\batch"] =
        [
            [
                new("B010_1.zip", @"D:\DbBackups\batch\B010_1.zip", Start, 10),
                new("B01_1.zip", @"D:\DbBackups\batch\B01_1.zip", Start, 10)
            ],
            [new("B01_1.zip", @"D:\DbBackups\batch\B01_1.zip", Start, 10)]
        ];
        var delay = new AdvancingDelay(clock);
        var service = new DbDownloadService(new FakeApiClient(), repository, clock, delay);

        var job = await service.RunAsync(
            new DbDownloaderSettings { BackupRootFolder = @"D:\DbBackups", TimeoutSeconds = 30 },
            ["B01"]);

        Assert.Equal(BranchBackupStatus.Ready, job.Items.Single().Status);
        Assert.Equal(@"D:\DbBackups\batch\B01_1.zip", job.Items.Single().RemoteZipPath);
    }

    [Fact]
    public async Task DownloadFailure_UsesStableSanitizedStateInsteadOfRawExceptionText()
    {
        var repository = new FakeBackupRepository
        {
            DownloadException = new IOException("password=secret \\\\server\\share\\private.zip")
        };
        var service = new DbDownloadService(new FakeApiClient(), repository);
        var item = new BranchBackupItem("B01")
        {
            RemoteZipPath = @"D:\DbBackups\batch\B01_1.zip",
            Status = BranchBackupStatus.Ready
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DownloadAsync(
            new DbDownloaderSettings { BackupRootFolder = @"D:\DbBackups" },
            item,
            Path.Combine(Path.GetTempPath(), "pos-admin-downloader-test")));

        Assert.Equal(BranchBackupStatus.Failed, item.Status);
        Assert.Equal(DownloaderFailureCodes.DownloadFailed, item.FailureCode);
        Assert.Equal("The branch archive could not be downloaded.", item.ErrorMessage);
        Assert.DoesNotContain("secret", item.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("server", item.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeApiClient : IBackupApiClient
    {
        public Task<DownloaderTriggerResult> TriggerBackupAsync(
            string apiUrl,
            IReadOnlyList<string> branchCodes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DownloaderTriggerResult(DownloaderTriggerState.Accepted));
    }

    private sealed class FakeBackupRepository : IBackupRepository
    {
        public IReadOnlyList<RemoteEntryInfo> Directories { get; init; } = [];

        public Dictionary<string, IReadOnlyList<RemoteEntryInfo>[]> FileSequences { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Exception? DownloadException { get; init; }

        private readonly Dictionary<string, int> _sequenceIndexes = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<RemoteEntryInfo>> ListDirectoriesAsync(RemoteConnectionInfo connection, string rootFolder, CancellationToken cancellationToken = default) =>
            Task.FromResult(Directories);

        public Task<IReadOnlyList<RemoteEntryInfo>> ListFilesAsync(RemoteConnectionInfo connection, string folder, CancellationToken cancellationToken = default)
        {
            if (!FileSequences.TryGetValue(folder, out var sequences) || sequences.Length == 0)
            {
                return Task.FromResult((IReadOnlyList<RemoteEntryInfo>)[]);
            }

            var index = _sequenceIndexes.TryGetValue(folder, out var current) ? current : 0;
            _sequenceIndexes[folder] = Math.Min(index + 1, sequences.Length - 1);
            return Task.FromResult(sequences[index]);
        }

        public Task DownloadFileAsync(RemoteConnectionInfo connection, string remoteFilePath, string localFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (DownloadException is not null) throw DownloadException;
            return Task.CompletedTask;
        }
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Current { get; private set; } = start;

        public override DateTimeOffset GetUtcNow() => Current;

        public void Advance(TimeSpan amount) => Current += amount;
    }

    private sealed class AdvancingDelay(ManualClock clock, Action? onDelay = null) : IDownloaderDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public TimeSpan Total => Delays.Aggregate(TimeSpan.Zero, (current, value) => current + value);

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            Delays.Add(delay);
            onDelay?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            clock.Advance(delay);
            return Task.CompletedTask;
        }
    }
}
