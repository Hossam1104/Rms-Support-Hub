namespace RmsSupportHub.Pos.Application.Diagnostics;

/// <summary>
/// Transport-neutral port for the Agent's existing bounded service-failure analyzer.
/// </summary>
public interface IServiceFailureAnalyzer
{
    Task<ServiceFailureAnalysis?> AnalyzeAsync(
        string serviceId,
        CancellationToken cancellationToken = default);
}
