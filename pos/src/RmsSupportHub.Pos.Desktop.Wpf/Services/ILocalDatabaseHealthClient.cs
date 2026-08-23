using RmsSupportHub.Pos.Desktop.Wpf.Models;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public interface ILocalDatabaseHealthClient
{
    Task<DatabaseHealthResult> GetHealthAsync(
        string correlationId,
        CancellationToken cancellationToken = default);
}
