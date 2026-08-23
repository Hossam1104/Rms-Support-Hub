using RmsSupportHub.Pos.Desktop.Wpf.Models;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public interface ILocalLogEvidenceClient
{
    Task<LogEvidenceResult> GetEvidenceAsync(
        string correlationId,
        CancellationToken cancellationToken = default);
}
