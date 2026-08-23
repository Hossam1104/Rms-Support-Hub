using RmsSupportHub.Pos.Contracts.V1.Artifacts;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.Desktop.Wpf.Services;
using RmsSupportHub.Pos.Desktop.Wpf.ViewModels;

namespace RmsSupportHub.Pos.Desktop.Wpf.Tests;

public sealed class LogsAndSupportBundleViewModelTests
{
    [Fact]
    public async Task LogsWorkspaceLoadsEvidenceOnOpenButNotOnHealthRefresh()
    {
        var logs = new StubLogEvidenceClient(_ => Task.FromResult(CreateEvidenceResult()));
        using var viewModel = CreateViewModel(logs: logs);

        await viewModel.RefreshAsync();

        Assert.Equal(0, logs.CallCount);
        Assert.True(viewModel.IsDashboardVisible);

        viewModel.ShowLogsCommand.Execute(null);
        await WaitUntilAsync(() => logs.CallCount == 1 && viewModel.LogEvidenceState == LogEvidenceViewState.Degraded);

        Assert.True(viewModel.IsLogsVisible);
        Assert.False(viewModel.IsDashboardVisible);
        Assert.Equal(3, viewModel.LogEvidenceServices.Count);
    }

    [Fact]
    public async Task RefreshCommandInLogsWorkspaceLoadsEvidenceWhileAutomaticHealthPollDoesNot()
    {
        var logs = new StubLogEvidenceClient(_ => Task.FromResult(CreateEvidenceResult()));
        using var viewModel = CreateViewModel(logs: logs, refreshInterval: TimeSpan.FromSeconds(5));

        await viewModel.RefreshAsync();
        viewModel.ShowLogsCommand.Execute(null);
        await WaitUntilAsync(() => logs.CallCount == 1);

        viewModel.RefreshCommand.Execute(null);
        await WaitUntilAsync(() => logs.CallCount == 2);

        viewModel.StartAutomaticRefresh();
        await Task.Delay(100);
        Assert.Equal(2, logs.CallCount);
    }

    [Fact]
    public async Task LogFiltersRemainClientSideAndUseSafeLabels()
    {
        var logs = new StubLogEvidenceClient(_ => Task.FromResult(CreateEvidenceResult()));
        using var viewModel = CreateViewModel(logs: logs);

        await viewModel.RefreshAsync();
        viewModel.ShowLogsCommand.Execute(null);
        await WaitUntilAsync(() => logs.CallCount == 1);

        viewModel.SelectedLogSeverityFilter = "Warning";
        Assert.Single(viewModel.FilteredLogServices);
        Assert.Equal("RMS Branch Service", viewModel.FilteredLogServices[0].DisplayName);

        viewModel.SelectedLogSeverityFilter = "All";
        viewModel.SelectedLogServiceFilter = "RMS Services Manager";
        Assert.Single(viewModel.FilteredLogServices);
        Assert.Equal("No failure evidence", viewModel.FilteredLogServices[0].CategoryLabel);
    }

    [Fact]
    public async Task SupportBundleCommandIsSingleFlightAndExposesOnlyMetadata()
    {
        var bundleStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bundle = new StubSupportBundleClient(async cancellationToken =>
        {
            bundleStarted.TrySetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return SupportBundleResult.Succeeded(
                new(
                    "0123456789abcdef0123456789abcdef",
                    "rms-support-bundle.zip",
                    1024,
                    new string('a', 64),
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddHours(1)),
                DateTimeOffset.UtcNow,
                "bundle-correlation",
                ["manifest", "health"]);
        });
        using var viewModel = CreateViewModel(bundle: bundle);
        await viewModel.RefreshAsync();

        var first = viewModel.GenerateSupportBundleCommand;
        first.Execute(null);
        await bundleStarted.Task;
        first.Execute(null);
        Assert.Equal(1, bundle.CallCount);
        Assert.False(first.CanExecute(null));

        release.SetResult(true);
        await WaitUntilAsync(() => viewModel.SupportBundleState == SupportBundleViewState.Succeeded);

        Assert.True(viewModel.IsSupportBundleResultVisible);
        Assert.Equal("rms-support-bundle.zip", viewModel.SupportBundleArtifactDisplayName);
        Assert.Equal(1024L.ToString("N0") + " bytes", viewModel.SupportBundleSizeDisplay);
        Assert.DoesNotContain(".zip", viewModel.SupportBundleArtifactId, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no UAC", viewModel.SupportBundleAuthorizationNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnauthorizedSupportBundleFailureRemainsSafeAndRetryable()
    {
        var bundle = new StubSupportBundleClient(_ => Task.FromResult(
            SupportBundleResult.Failure(
                SupportBundleViewState.Unauthorized,
                "administrator_authorization_required",
                "Administrator authority is required to generate a Support Bundle.")));
        using var viewModel = CreateViewModel(bundle: bundle);
        await viewModel.RefreshAsync();

        viewModel.GenerateSupportBundleCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.SupportBundleState == SupportBundleViewState.Unauthorized);

        Assert.Equal("administrator_authorization_required", viewModel.SupportBundleErrorCode);
        Assert.Contains("Administrator authority", viewModel.SupportBundleStatusSummary, StringComparison.Ordinal);
        Assert.True(viewModel.CanGenerateSupportBundle);
    }

    private static DashboardViewModel CreateViewModel(
        StubLogEvidenceClient? logs = null,
        StubSupportBundleClient? bundle = null,
        TimeSpan? refreshInterval = null) =>
        new(
            new StubHealthClient(),
            new StubServiceHealthClient(),
            new StubDatabaseHealthClient(),
            logs ?? new StubLogEvidenceClient(_ => Task.FromResult(CreateEvidenceResult())),
            bundle ?? new StubSupportBundleClient(_ => Task.FromResult(
                SupportBundleResult.Failure(
                    SupportBundleViewState.Unavailable,
                    "agent_unavailable",
                    "RMS Support Agent is not available on this machine."))),
            refreshInterval);

    private static LogEvidenceResult CreateEvidenceResult() => LogEvidenceResult.Success(
        LogEvidenceViewState.Degraded,
        [
            new(
                "svc-branch",
                "RMS Branch Service",
                FailureCategory.ServiceStopped,
                FailureSeverity.Warning,
                FailureConfidence.High,
                "The Branch service has bounded evidence.",
                [new("SCM", DateTimeOffset.UtcNow, "The service is stopped.", null, [], "7000")],
                [],
                [new("restart-review", "Review service state", "Review the bounded evidence.")]),
            new(
                "svc-cashier",
                "RMS Cashier Service",
                FailureCategory.Crash,
                FailureSeverity.ActionRequired,
                FailureConfidence.Medium,
                "The Cashier service has bounded evidence.",
                [new("Application Event Log", DateTimeOffset.UtcNow, "A bounded application event was reported.", "System.Exception", [], "1000")],
                [],
                []),
            new(
                "svc-manager",
                "RMS Services Manager",
                FailureCategory.None,
                FailureSeverity.Informational,
                FailureConfidence.High,
                "No bounded failure evidence was reported.",
                [],
                [],
                [])
        ],
        DateTimeOffset.UtcNow,
        "logs-correlation");

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition(), "The expected asynchronous workspace state was not reached.");
    }

    private sealed class StubHealthClient : ILocalAgentHealthClient
    {
        public Task<AgentHealthResult> GetHealthAsync(string correlationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(AgentHealthResult.Connected("Ready", "Ready", 1, false, correlationId));
    }

    private sealed class StubServiceHealthClient : ILocalServiceHealthClient
    {
        public Task<ServiceHealthResult> GetHealthAsync(string correlationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceHealthResult.Healthy(
                ServiceHealthViewState.Healthy,
                [],
                DateTimeOffset.UtcNow,
                correlationId));
    }

    private sealed class StubDatabaseHealthClient : ILocalDatabaseHealthClient
    {
        public Task<DatabaseHealthResult> GetHealthAsync(string correlationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(DatabaseHealthResult.Success(
                DatabaseHealthViewState.Healthy,
                [],
                DateTimeOffset.UtcNow,
                correlationId));
    }

    private sealed class StubLogEvidenceClient(
        Func<CancellationToken, Task<LogEvidenceResult>> responder) : ILocalLogEvidenceClient
    {
        public int CallCount { get; private set; }

        public async Task<LogEvidenceResult> GetEvidenceAsync(
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return await responder(cancellationToken);
        }
    }

    private sealed class StubSupportBundleClient(
        Func<CancellationToken, Task<SupportBundleResult>> responder) : ILocalSupportBundleClient
    {
        public int CallCount { get; private set; }

        public async Task<SupportBundleResult> GenerateAsync(
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return await responder(cancellationToken);
        }
    }

}
