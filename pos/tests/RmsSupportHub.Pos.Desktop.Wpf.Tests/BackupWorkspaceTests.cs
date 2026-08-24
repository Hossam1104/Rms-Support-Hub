using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.Desktop.Wpf.Services;
using RmsSupportHub.Pos.Desktop.Wpf.ViewModels;

namespace RmsSupportHub.Pos.Desktop.Wpf.Tests;

public sealed class BackupWorkspaceTests
{
    private static readonly DateTimeOffset Created = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BackupNavigationLoadsBoundedInventoryAndKeepsOperatorReadOnly()
    {
        var backup = new StubBackupClient(canCreate: false, canExport: false);
        using var viewModel = CreateViewModel(backup);

        viewModel.ShowBackupCommand.Execute(null);
        await backup.InventoryCompleted.Task;

        Assert.True(viewModel.IsBackupVisible);
        Assert.Single(viewModel.BackupItems);
        Assert.Equal(BackupViewState.Succeeded, viewModel.BackupState);
        Assert.False(viewModel.CanCreateBackup);
        Assert.False(viewModel.CanExportBackup);
        Assert.False(viewModel.CreateBranchBackupCommand.CanExecute(null));
        Assert.Equal("Local operator access is read-only. Administrator authority is required to create or export artifacts.", viewModel.BackupAuthorizationNote);
    }

    [Fact]
    public async Task AdministratorCanCreateBackupAndInventoryRefreshesAfterSuccess()
    {
        var backup = new StubBackupClient(canCreate: true, canExport: true)
        {
            CreateResult = new(
                BackupViewState.Succeeded,
                null,
                string.Empty,
                string.Empty)
        };
        using var viewModel = CreateViewModel(backup);

        viewModel.ShowBackupCommand.Execute(null);
        await backup.InventoryCompleted.Task;
        Assert.True(viewModel.CreateBranchBackupCommand.CanExecute(null));

        viewModel.CreateBranchBackupCommand.Execute(null);
        await backup.CreateCompleted.Task;
        await WaitUntilAsync(() => backup.InventoryCallCount >= 2);

        Assert.Equal(RmsDatabaseTarget.Branch, backup.CreatedTargets.Single());
        Assert.Equal(BackupViewState.Succeeded, viewModel.BackupState);
    }

    [Fact]
    public async Task ExportCommandUsesSaveDialogThenExplicitOverwriteConfirmation()
    {
        var backup = new StubBackupClient(canCreate: true, canExport: true)
        {
            ExportResults =
            [
                ArtifactExportResult.Failure(
                    ArtifactExportViewState.DestinationExists,
                    "destination_exists",
                    "The destination exists.",
                    "0123456789abcdef0123456789abcdef"),
                new(
                    ArtifactExportViewState.Succeeded,
                    "0123456789abcdef0123456789abcdef",
                    "branch.bak",
                    10,
                    new string('a', 64),
                    "downloads",
                    string.Empty,
                    string.Empty)
            ]
        };
        var picker = new StubDestinationPicker(@"C:\Users\Test\Downloads\branch.bak");
        var confirmation = new StubOverwriteConfirmation(true);
        using var viewModel = CreateViewModel(backup, picker, confirmation);

        viewModel.ShowBackupCommand.Execute(null);
        await backup.InventoryCompleted.Task;
        viewModel.ExportBackupCommand.Execute(viewModel.BackupItems[0]);
        await backup.ExportCompleted.Task;

        Assert.Equal(ArtifactExportViewState.Succeeded, viewModel.ArtifactExport.State);
        Assert.Equal(2, backup.ExportRequests.Count);
        Assert.False(backup.ExportRequests[0].OverwriteConfirmed);
        Assert.True(backup.ExportRequests[1].OverwriteConfirmed);
        Assert.Equal(1, confirmation.CallCount);
        Assert.Equal("branch.bak", picker.SuggestedFileName);
    }

    private static DashboardViewModel CreateViewModel(
        StubBackupClient backup,
        ILocalArtifactDestinationPicker? picker = null,
        IOverwriteConfirmation? confirmation = null) =>
        new(
            new ConnectedHealthClient(),
            new HealthyServiceClient(),
            new HealthyDatabaseClient(),
            new EmptyLogClient(),
            new EmptySupportBundleClient(),
            backup,
            picker ?? new StubDestinationPicker(@"C:\Users\Test\Downloads\backup.bak"),
            confirmation ?? new StubOverwriteConfirmation(false));

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (predicate()) return;
            await Task.Delay(10);
        }

        Assert.True(predicate(), "The asynchronous WPF command did not complete in time.");
    }

    private sealed class StubBackupClient(bool canCreate, bool canExport) : ILocalBackupClient
    {
        public int InventoryCallCount { get; private set; }
        public List<RmsDatabaseTarget> CreatedTargets { get; } = [];
        public List<LocalArtifactExportRequest> ExportRequests { get; } = [];
        public TaskCompletionSource<bool> InventoryCompleted { get; } = NewCompletionSource();
        public TaskCompletionSource<bool> CreateCompleted { get; } = NewCompletionSource();
        public TaskCompletionSource<bool> ExportCompleted { get; } = NewCompletionSource();
        public BackupOperationResult CreateResult { get; set; } = BackupOperationResult.Failure(BackupViewState.Failed, "not-configured", "not configured");
        public IReadOnlyList<ArtifactExportResult> ExportResults { get; set; } = [];

        public Task<BackupInventoryResult> GetInventoryAsync(CancellationToken cancellationToken = default)
        {
            InventoryCallCount++;
            InventoryCompleted.TrySetResult(true);
            return Task.FromResult(new BackupInventoryResult(
                BackupViewState.Succeeded,
                [CreateRow()],
                Created,
                string.Empty,
                string.Empty,
                true,
                canCreate,
                canExport));
        }

        public Task<BackupOperationResult> CreateAsync(RmsDatabaseTarget target, CancellationToken cancellationToken = default)
        {
            CreatedTargets.Add(target);
            CreateCompleted.TrySetResult(true);
            return Task.FromResult(CreateResult);
        }

        public Task<ArtifactExportResult> ExportAsync(LocalArtifactExportRequest request, CancellationToken cancellationToken = default)
        {
            ExportRequests.Add(request);
            var index = ExportRequests.Count - 1;
            var result = index < ExportResults.Count
                ? ExportResults[index]
                : ArtifactExportResult.Failure(ArtifactExportViewState.Failed, "unexpected", "unexpected", request.ArtifactId);
            ExportCompleted.TrySetResult(true);
            return Task.FromResult(result);
        }

        private static TaskCompletionSource<bool> NewCompletionSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private static BackupArtifactRow CreateRow() =>
            new(
                RmsDatabaseTarget.Branch,
                "0123456789abcdef0123456789abcdef",
                "branch.bak",
                10,
                new string('a', 64),
                Created,
                Created.AddDays(1),
                LocalIpcBackupAvailability.Available);
    }

    private sealed class ConnectedHealthClient : ILocalAgentHealthClient
    {
        public Task<AgentHealthResult> GetHealthAsync(string correlationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(AgentHealthResult.Connected("Ready", "Ready", 1, false, correlationId));
    }

    private sealed class HealthyServiceClient : ILocalServiceHealthClient
    {
        public Task<ServiceHealthResult> GetHealthAsync(string correlationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceHealthResult.Healthy(ServiceHealthViewState.Healthy, [], DateTimeOffset.UtcNow, correlationId));
    }

    private sealed class HealthyDatabaseClient : ILocalDatabaseHealthClient
    {
        public Task<DatabaseHealthResult> GetHealthAsync(string correlationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(DatabaseHealthResult.Success(DatabaseHealthViewState.Healthy, [], DateTimeOffset.UtcNow, correlationId));
    }

    private sealed class EmptyLogClient : ILocalLogEvidenceClient
    {
        public Task<LogEvidenceResult> GetEvidenceAsync(string correlationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(LogEvidenceResult.Success(LogEvidenceViewState.Healthy, [], DateTimeOffset.UtcNow, correlationId));
    }

    private sealed class EmptySupportBundleClient : ILocalSupportBundleClient
    {
        public Task<SupportBundleResult> GenerateAsync(string correlationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(SupportBundleResult.Failure(SupportBundleViewState.Unavailable, "unavailable", "unavailable", correlationId));
    }

    private sealed class StubDestinationPicker(string path) : ILocalArtifactDestinationPicker
    {
        public string? SuggestedFileName { get; private set; }

        public Task<string?> PickAsync(string suggestedFileName, string extension, CancellationToken cancellationToken = default)
        {
            SuggestedFileName = suggestedFileName;
            return Task.FromResult<string?>(path);
        }
    }

    private sealed class StubOverwriteConfirmation(bool result) : IOverwriteConfirmation
    {
        public int CallCount { get; private set; }

        public Task<bool> ConfirmAsync(string displayName, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }
}
