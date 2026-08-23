using RmsSupportHub.Pos.Application.Diagnostics;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

public sealed class LogEvidenceQueryTests
{
    [Fact]
    public async Task LocalOperatorReceivesOnlyTheFixedBoundedServiceProjection()
    {
        var analyzer = new FakeAnalyzer((serviceId, _) =>
            Task.FromResult<ServiceFailureAnalysisDto?>(CreateAnalysis(serviceId, bounded: true)));
        var handler = new LogEvidenceQueryHandler(analyzer, TimeProvider.System);

        var result = await handler.HandleAsync(LocalOperatorContext("logs-bounded"));

        Assert.True(result.Succeeded);
        var snapshot = Assert.IsType<LogEvidenceSnapshotDto>(result.Value);
        Assert.Equal(LogEvidenceOverallState.Degraded, snapshot.OverallState);
        Assert.Equal(3, snapshot.Services.Count);
        Assert.Equal(
            RmsServiceCatalog.Definitions.Select(definition => ServiceIdentityCatalog.ToServiceId(definition.ServiceName)),
            snapshot.Services.Select(service => service.ServiceId));
        Assert.All(snapshot.Services, service =>
        {
            Assert.Equal(12, service.Evidence.Count);
            Assert.Equal(8, service.UnknownReasons.Count);
            Assert.Equal(4, service.Recommendations.Count);
        });
        Assert.Equal(3, analyzer.CallCount);
    }

    [Fact]
    public async Task LocalAdministratorCanUseTheSameDiagnosticProjection()
    {
        var handler = new LogEvidenceQueryHandler(
            new FakeAnalyzer((serviceId, _) =>
                Task.FromResult<ServiceFailureAnalysisDto?>(CreateAnalysis(serviceId))),
            TimeProvider.System);

        var result = await handler.HandleAsync(new InvocationContext(
            InvocationSource.LocalWpf,
            "local-admin",
            InvocationAuthorizationLevel.LocalAdministrator,
            "logs-admin"));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task MissingUnauthenticatedAndRemoteContextsAreDeniedBeforeAnalyzerCalls()
    {
        var analyzer = new FakeAnalyzer((serviceId, _) =>
            Task.FromResult<ServiceFailureAnalysisDto?>(CreateAnalysis(serviceId)));
        var handler = new LogEvidenceQueryHandler(analyzer, TimeProvider.System);

        var missing = await handler.HandleAsync(null);
        var unauthenticated = await handler.HandleAsync(new InvocationContext(
            InvocationSource.LocalWpf,
            "unauthenticated",
            InvocationAuthorizationLevel.Unauthenticated,
            "logs-unauthenticated"));
        var remote = await handler.HandleAsync(new InvocationContext(
            InvocationSource.RemoteHub,
            "remote-admin",
            InvocationAuthorizationLevel.RemoteAdministrator,
            "logs-remote"));

        Assert.Equal("invocation_context_missing", missing.Error?.Code);
        Assert.Equal("diagnostic_authorization_required", unauthenticated.Error?.Code);
        Assert.Equal("diagnostic_authorization_required", remote.Error?.Code);
        Assert.Equal(0, analyzer.CallCount);
    }

    [Fact]
    public async Task ContradictoryOrSensitiveAnalyzerOutputFailsClosed()
    {
        var analyzer = new FakeAnalyzer((serviceId, _) =>
        {
            var analysis = CreateAnalysis(serviceId);
            return Task.FromResult<ServiceFailureAnalysisDto?>(
                serviceId == ServiceIdentityCatalog.ToServiceId(RmsServiceCatalog.BranchServiceName)
                    ? analysis with { ServiceDisplayName = "Unexpected service" }
                    : analysis with { Summary = "password=secret" });
        });
        var handler = new LogEvidenceQueryHandler(analyzer, TimeProvider.System);

        var result = await handler.HandleAsync(LocalOperatorContext("logs-invalid"));

        Assert.False(result.Succeeded);
        Assert.Equal("logs_evidence_unavailable", result.Error?.Code);
        Assert.DoesNotContain("password", result.Error?.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result.Error?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzerTimeoutIsMappedToFixedSafeError()
    {
        var analyzer = new FakeAnalyzer(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return null;
        });
        var handler = new LogEvidenceQueryHandler(
            analyzer,
            TimeProvider.System,
            TimeSpan.FromMilliseconds(20));

        var result = await handler.HandleAsync(LocalOperatorContext("logs-timeout"));

        Assert.False(result.Succeeded);
        Assert.Equal("logs_evidence_timeout", result.Error?.Code);
        Assert.Equal("RMS diagnostic evidence timed out.", result.Error?.Message);
    }

    [Fact]
    public async Task CallerCancellationIsPreserved()
    {
        var analyzer = new FakeAnalyzer(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return null;
        });
        var handler = new LogEvidenceQueryHandler(analyzer, TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync(LocalOperatorContext("logs-cancelled"), cancellation.Token));
    }

    private static InvocationContext LocalOperatorContext(string correlationId) => new(
        InvocationSource.LocalWpf,
        "local-operator",
        InvocationAuthorizationLevel.LocalOperator,
        correlationId);

    private static ServiceFailureAnalysisDto CreateAnalysis(string serviceId, bool bounded = false)
    {
        var definition = RmsServiceCatalog.Definitions.Single(item =>
            ServiceIdentityCatalog.ToServiceId(item.ServiceName) == serviceId);
        return new(
            serviceId,
            definition.DisplayName,
            FailureCategory.ServiceStopped,
            FailureSeverity.ActionRequired,
            FailureConfidence.High,
            "The fixed service has bounded diagnostic evidence.",
            DateTimeOffset.UtcNow,
            bounded
                ? Enumerable.Range(0, 12)
                    .Select(index => new FailureEvidenceDto(
                        "SCM",
                        DateTimeOffset.UtcNow,
                        $"Bounded evidence {index}",
                        null,
                        [],
                        "7000"))
                    .ToArray()
                : [new("SCM", DateTimeOffset.UtcNow, "Bounded evidence", null, [], "7000")],
            bounded
                ? Enumerable.Range(0, 8).Select(index => $"Unknown reason {index}").ToArray()
                : [],
            bounded
                ? Enumerable.Range(0, 4)
                    .Select(index => new FailureRecommendationDto(
                        $"rec-{index}",
                        $"Recommendation {index}",
                        "Review the bounded evidence."))
                    .ToArray()
                : []);
    }

    private sealed class FakeAnalyzer(
        Func<string, CancellationToken, Task<ServiceFailureAnalysisDto?>> responder)
        : IServiceFailureAnalyzer
    {
        public int CallCount => Volatile.Read(ref callCount);

        private int callCount;

        public async Task<ServiceFailureAnalysisDto?> AnalyzeAsync(
            string serviceId,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref callCount);
            return await responder(serviceId, cancellationToken);
        }
    }
}
