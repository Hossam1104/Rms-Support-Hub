using System.Security.Cryptography;
using System.Text;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Services;

/// <summary>
/// Projects the server-owned canonical RMS service catalog into opaque IDs. Browser input is
/// accepted only as one of those IDs; the Windows service name never crosses the HTTP boundary as
/// a caller-selected target.
/// </summary>
public sealed class ServiceAllowList
{
    public async Task<IReadOnlyList<AllowListedService>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return RmsServiceCatalog.Definitions
            .Select(definition => new AllowListedService(
                ToServiceId(definition.ServiceName),
                definition.ServiceName,
                definition.DisplayName))
            .ToArray();
    }

    public async Task<AllowListedService?> ResolveAsync(
        string? serviceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsOpaqueServiceId(serviceId))
        {
            return null;
        }

        var services = await GetAsync(cancellationToken).ConfigureAwait(false);
        return services.FirstOrDefault(service =>
            string.Equals(service.ServiceId, serviceId, StringComparison.Ordinal));
    }

    public static string ToServiceId(string serviceName) =>
        "svc-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serviceName.Trim())))
            .ToLowerInvariant()[..16];

    public static bool IsOpaqueServiceId(string? serviceId) =>
        serviceId is { Length: 20 }
        && serviceId.StartsWith("svc-", StringComparison.Ordinal)
        && serviceId[4..].All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

}

public sealed record AllowListedService(string ServiceId, string ServiceName, string DisplayName);
