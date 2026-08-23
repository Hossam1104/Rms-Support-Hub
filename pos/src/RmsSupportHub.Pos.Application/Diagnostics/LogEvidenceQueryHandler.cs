using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Diagnostics;

/// <summary>
/// Shared, transport-neutral query for bounded evidence from the fixed RMS service catalog.
/// Detailed evidence is on-demand and intentionally not part of the periodic health refresh.
/// </summary>
public sealed class LogEvidenceQueryHandler
{
    public const string Operation = "rms.logs.evidence";
    public const int MaximumEvidenceRecordsPerService = 12;
    public const int MaximumUnknownReasonsPerService = 8;
    public const int MaximumRecommendationsPerService = 4;
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private static readonly IReadOnlyList<(string ServiceId, string DisplayName)> FixedServices =
        RmsServiceCatalog.Definitions
            .Select(definition => (
                ServiceIdentityCatalog.ToServiceId(definition.ServiceName),
                definition.DisplayName))
            .ToArray();

    private readonly IServiceFailureAnalyzer analyzer;
    private readonly TimeProvider clock;
    private readonly TimeSpan timeout;

    public LogEvidenceQueryHandler(
        IServiceFailureAnalyzer analyzer,
        TimeProvider clock,
        TimeSpan? timeout = null)
    {
        this.analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.timeout = timeout ?? DefaultTimeout;
        if (this.timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
    }

    public async Task<ApplicationResult<LogEvidenceSnapshot>> HandleAsync(
        InvocationContext? context,
        CancellationToken cancellationToken = default)
    {
        var decision = AgentOperationAuthorization.Authorize(
            context,
            AgentOperationRisk.ReadOnlyDiagnostic);
        if (!decision.Allowed)
        {
            return ApplicationResult<LogEvidenceSnapshot>.Failure(
                decision.Code,
                decision.Message);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            var tasks = FixedServices
                .Select(service => analyzer.AnalyzeAsync(service.ServiceId, timeoutSource.Token))
                .ToArray();
            var analyses = await Task.WhenAll(tasks)
                .WaitAsync(timeoutSource.Token)
                .ConfigureAwait(false);

            if (analyses.Length != FixedServices.Count
                || analyses.Any(analysis => analysis is null)
                || !TryValidateAnalyses(analyses!, out var safeAnalyses))
            {
                return ApplicationResult<LogEvidenceSnapshot>.Failure(
                    "logs_evidence_unavailable",
                    "RMS diagnostic evidence is currently unavailable.");
            }

            return ApplicationResult<LogEvidenceSnapshot>.Success(
                new(
                    clock.GetUtcNow(),
                    DetermineOverallState(safeAnalyses),
                    safeAnalyses));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            return ApplicationResult<LogEvidenceSnapshot>.Failure(
                "logs_evidence_timeout",
                "RMS diagnostic evidence timed out.");
        }
        catch
        {
            return ApplicationResult<LogEvidenceSnapshot>.Failure(
                "logs_evidence_unavailable",
                "RMS diagnostic evidence is currently unavailable.");
        }
    }

    private static LogEvidenceOverallState DetermineOverallState(
        IReadOnlyList<ServiceFailureAnalysis> analyses)
    {
        if (analyses.Count != FixedServices.Count)
        {
            return LogEvidenceOverallState.Unknown;
        }

        if (analyses.All(analysis =>
                analysis.Category == FailureCategory.None
                && analysis.UnknownReasons.Count == 0))
        {
            return LogEvidenceOverallState.Healthy;
        }

        if (analyses.All(analysis =>
                analysis.Category == FailureCategory.Unknown
                && analysis.Evidence.Count == 0))
        {
            return LogEvidenceOverallState.Unavailable;
        }

        return LogEvidenceOverallState.Degraded;
    }

    private static bool TryValidateAnalyses(
        IReadOnlyList<ServiceFailureAnalysis?> analyses,
        out IReadOnlyList<ServiceFailureAnalysis> safeAnalyses)
    {
        safeAnalyses = [];
        if (analyses.Count != FixedServices.Count)
        {
            return false;
        }

        var values = new List<ServiceFailureAnalysis>(FixedServices.Count);
        foreach (var (expected, analysis) in FixedServices.Zip(analyses))
        {
            if (analysis is null
                || !string.Equals(analysis.ServiceId, expected.ServiceId, StringComparison.Ordinal)
                || !string.Equals(analysis.ServiceDisplayName, expected.DisplayName, StringComparison.Ordinal)
                || !Enum.IsDefined(analysis.Category)
                || !Enum.IsDefined(analysis.Severity)
                || !Enum.IsDefined(analysis.Confidence)
                || analysis.CheckedAtUtc == default
                || !IsSafeText(analysis.Summary, 512)
                || analysis.Evidence is null
                || analysis.Evidence.Count > MaximumEvidenceRecordsPerService
                || analysis.UnknownReasons is null
                || analysis.UnknownReasons.Count > MaximumUnknownReasonsPerService
                || analysis.Recommendations is null
                || analysis.Recommendations.Count > MaximumRecommendationsPerService)
            {
                return false;
            }

            if (analysis.Evidence.Any(evidence =>
                    !IsSafeText(evidence.Source, 128)
                    || evidence.AtUtc is { } atUtc && atUtc == default
                    || !IsSafeText(evidence.Summary, 512)
                    || !IsSafeText(evidence.ExceptionType, 128)
                    || evidence.StackFrames is null
                    || evidence.StackFrames.Count > 12
                    || evidence.StackFrames.Any(frame => !IsSafeText(frame, 256))
                    || !IsSafeText(evidence.EventId, 64))
                || analysis.UnknownReasons.Any(reason => !IsSafeText(reason, 256))
                || analysis.Recommendations.Any(recommendation =>
                    !IsSafeText(recommendation.Code, 64)
                    || !IsSafeText(recommendation.Label, 128)
                    || !IsSafeText(recommendation.Summary, 512)))
            {
                return false;
            }

            values.Add(analysis);
        }

        safeAnalyses = values;
        return true;
    }

    private static bool IsSafeText(string? value, int maximumLength)
    {
        if (value is null)
        {
            return true;
        }

        return value.Length <= maximumLength
            && !value.Any(char.IsControl)
            && !value.Contains("connectionstring", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("password", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            && !value.Contains(":" + (char)92, StringComparison.Ordinal);
    }
}
