using RmsSupportHub.Pos.Desktop.Wpf.Models;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public interface ILocalSupportBundleClient
{
    Task<SupportBundleResult> GenerateAsync(
        string correlationId,
        CancellationToken cancellationToken = default);
}
