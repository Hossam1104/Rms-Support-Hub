using RmsSupportHub.Pos.Desktop.Wpf.Models;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public interface ILocalAgentHealthClient
{
    Task<AgentHealthResult> GetHealthAsync(
        string correlationId,
        CancellationToken cancellationToken = default);
}
