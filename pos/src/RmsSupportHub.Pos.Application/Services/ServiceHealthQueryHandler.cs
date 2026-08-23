using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Services;

/// <summary>
/// Shared transport-agnostic, read-only service-health operation. It is intentionally non-audited
/// because WPF polls it periodically; service mutation remains a separate audited capability.
/// </summary>
public sealed class ServiceHealthQueryHandler(ServiceHealthReader reader)
{
    public const string Operation = "rms.services.health";

    public async Task<ApplicationResult<ServiceHealthSnapshot>> HandleAsync(
        InvocationContext? context,
        CancellationToken cancellationToken = default)
    {
        var decision = AgentOperationAuthorization.Authorize(
            context,
            AgentOperationRisk.ReadOnlyDiagnostic);
        if (!decision.Allowed)
        {
            return ApplicationResult<ServiceHealthSnapshot>.Failure(decision.Code, decision.Message);
        }

        try
        {
            return ApplicationResult<ServiceHealthSnapshot>.Success(
                await reader.GetAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ServiceHealthLookupTimeoutException)
        {
            return ApplicationResult<ServiceHealthSnapshot>.Failure(
                "service_health_timeout",
                "Service health check timed out.");
        }
        catch
        {
            return ApplicationResult<ServiceHealthSnapshot>.Failure(
                "service_health_unavailable",
                "The RMS service health query could not be completed.");
        }
    }
}
