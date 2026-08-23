using RmsSupportHub.Pos.Desktop.Wpf.Models;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public interface ILocalServiceHealthClient
{
    Task<ServiceHealthResult> GetHealthAsync(
        string correlationId,
        CancellationToken cancellationToken = default);
}
