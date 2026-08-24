using RmsSupportHub.Pos.Contracts.V1.Artifacts;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.Desktop.Wpf.Services;
using RmsSupportHub.Pos.Desktop.Wpf.ViewModels;

namespace RmsSupportHub.Pos.Desktop.Wpf.Tests;

public sealed class ServiceControlWorkspaceTests
{
    [Fact]
    public async Task LocalOperatorCannotExecuteServiceActions()
    {
        var branch = CreateRow("svc-0123456789abcdef", ServiceHealthRowState.Stopped, canControl: true);
        using var viewModel = CreateViewModel(
            [branch],
            new StubServiceControlClient(
                new ServiceControlAuthorizationResult(false, false, string.Empty, string.Empty)));

        await viewModel.RefreshAsync();

        Assert.False(viewModel.CanManageRmsServices);
        Assert.False(viewModel.StartActionCommand.CanExecute(branch));
        Assert.False(viewModel.StopActionCommand.CanExecute(branch));
        Assert.False(viewModel.RestartActionCommand.CanExecute(branch));
    }

    [Fact]
    public async Task LocalAdministratorCanExecuteOnlyFixedControlRows()
    {
        var branch = CreateRow("svc-0123456789abcdef", ServiceHealthRowState.Stopped, canControl: true);
        var agent = CreateRow("svc-fedcba9876543210", ServiceHealthRowState.Running, canControl: false);
        using var viewModel = CreateViewModel(
            [branch, agent],
            new StubServiceControlClient(
                new ServiceControlAuthorizationResult(true, true, string.Empty, string.Empty)));

        await viewModel.RefreshAsync();

        Assert.True(viewModel.CanManageRmsServices);
        Assert.True(viewModel.StartActionCommand.CanExecute(branch));
        Assert.False(viewModel.StopActionCommand.CanExecute(branch));
        Assert.False(viewModel.RestartActionCommand.CanExecute(branch));
        Assert.False(viewModel.StartActionCommand.CanExecute(agent));
        Assert.False(viewModel.StopActionCommand.CanExecute(agent));
        Assert.False(viewModel.RestartActionCommand.CanExecute(agent));
    }

    [Fact]
    public void ServiceHealthRowPreservesAgentControlCapability()
    {
        var dto = new ServiceHealthItemDto(
            "svc-0123456789abcdef",
            "RMS Branch Service",
            true,
            true,
            ServiceRuntimeState.Stopped,
            "stopped",
            true);

        Assert.True(ServiceHealthRow.TryCreate(dto, out var row));
        Assert.NotNull(row);
        Assert.Equal(dto.ServiceId, row!.ServiceId);
        Assert.True(row.CanControl);
    }

    private static DashboardViewModel CreateViewModel(
        IReadOnlyList<ServiceHealthRow> rows,
        ILocalServiceControlClient controlClient) =>
        new(
            new StubHealthClient(),
            new StubServiceHealthClient(rows),
            new StubDatabaseHealthClient(),
            new StubLogEvidenceClient(),
            new StubSupportBundleClient(),
            new StubBackupClient(),
            new StubDestinationPicker(),
            new StubOverwriteConfirmation(),
            TimeSpan.FromSeconds(5),
            controlClient,
            new StubActionConfirmation());

    private static ServiceHealthRow CreateRow(
        string serviceId,
        ServiceHealthRowState state,
        bool canControl) => new(
            "RMS Branch Service",
            true,
            true,
            state,
            state == ServiceHealthRowState.Running ? "running" : "stopped")
    {
        ServiceId = serviceId,
        CanControl = canControl
    };

    private sealed class StubHealthClient : ILocalAgentHealthClient
    {
        public Task<AgentHealthResult> GetHealthAsync(
            string correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AgentHealthResult.Connected("Ready", "Ready", 1, false, correlationId));
    }

    private sealed class StubServiceHealthClient(IReadOnlyList<ServiceHealthRow> rows) : ILocalServiceHealthClient
    {
        public Task<ServiceHealthResult> GetHealthAsync(
            string correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceHealthResult.Healthy(
                ServiceHealthViewState.Healthy,
                rows,
                DateTimeOffset.UtcNow,
                correlationId));
    }

    private sealed class StubDatabaseHealthClient : ILocalDatabaseHealthClient
    {
        public Task<DatabaseHealthResult> GetHealthAsync(
            string correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DatabaseHealthResult.Failure(
                DatabaseHealthViewState.Unavailable,
                "not_tested",
                "Database health was not requested by this test.",
                correlationId));
    }

    private sealed class StubLogEvidenceClient : ILocalLogEvidenceClient
    {
        public Task<LogEvidenceResult> GetEvidenceAsync(
            string correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(LogEvidenceResult.Failure(
                LogEvidenceViewState.Unavailable,
                "not_tested",
                "Log evidence was not requested by this test.",
                correlationId));
    }

    private sealed class StubSupportBundleClient : ILocalSupportBundleClient
    {
        public Task<SupportBundleResult> GenerateAsync(
            string correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SupportBundleResult.Failure(
                SupportBundleViewState.Failed,
                "not_tested",
                "Support bundle generation was not requested by this test.",
                correlationId));
    }

    private sealed class StubBackupClient : ILocalBackupClient
    {
        public Task<BackupInventoryResult> GetInventoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(BackupInventoryResult.Failure(
                BackupViewState.Unavailable,
                "not_tested",
                "Backup inventory was not requested by this test."));

        public Task<BackupOperationResult> CreateAsync(
            RmsDatabaseTarget target,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(BackupOperationResult.Failure(
                BackupViewState.Failed,
                "not_tested",
                "Backup creation was not requested by this test."));

        public Task<ArtifactExportResult> ExportAsync(
            LocalArtifactExportRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ArtifactExportResult.Failure(
                ArtifactExportViewState.Failed,
                "not_tested",
                "Artifact export was not requested by this test."));
    }

    private sealed class StubDestinationPicker : ILocalArtifactDestinationPicker
    {
        public Task<string?> PickAsync(
            string suggestedFileName,
            string extension,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class StubOverwriteConfirmation : IOverwriteConfirmation
    {
        public Task<bool> ConfirmAsync(
            string displayName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class StubActionConfirmation : IServiceActionConfirmation
    {
        public Task<bool> ConfirmAsync(
            string displayName,
            ServiceActionKind action,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class StubServiceControlClient(ServiceControlAuthorizationResult authorization)
        : ILocalServiceControlClient
    {
        public Task<ServiceControlAuthorizationResult> GetAuthorizationAsync(
            string correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(authorization);

        public Task<ServiceActionResult> ExecuteAsync(
            string serviceId,
            ServiceActionKind action,
            string? confirmation,
            string correlationId,
            string idempotencyKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceActionResult.Failure(
                ServiceControlViewState.Failed,
                "not_tested",
                "Service action execution was not requested by this test.",
                serviceId,
                action,
                correlationId));
    }
}
