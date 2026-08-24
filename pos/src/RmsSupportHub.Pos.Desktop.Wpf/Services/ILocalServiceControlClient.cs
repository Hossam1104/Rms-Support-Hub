using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Desktop.Wpf.Models;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public interface ILocalServiceControlClient
{
    Task<ServiceControlAuthorizationResult> GetAuthorizationAsync(
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ServiceActionResult> ExecuteAsync(
        string serviceId,
        ServiceActionKind action,
        string? confirmation,
        string correlationId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
