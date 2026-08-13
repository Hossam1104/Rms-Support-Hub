using System.Security.Cryptography;
using System.Text;
using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Agent.Services;

/// <summary>
/// Projects the service names owned by Agent configuration into opaque IDs. Browser input is
/// accepted only as one of those IDs; the Windows service name never crosses the HTTP boundary as
/// a caller-selected target.
/// </summary>
public sealed class ServiceAllowList(IAgentConfigurationStore configurations)
{
    public async Task<IReadOnlyList<AllowListedService>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var configuration = await configurations.LoadAsync(cancellationToken).ConfigureAwait(false);
        return Build(configuration.Services);
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

    private static IReadOnlyList<AllowListedService> Build(IEnumerable<string>? configuredNames)
    {
        var names = (configuredNames ?? [])
            .Where(IsSafeConfiguredServiceName)
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        return names
            .Select(name => new AllowListedService(ToServiceId(name), name))
            .ToArray();
    }

    private static bool IsSafeConfiguredServiceName(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= 256
        && name.All(character => !char.IsControl(character))
        && !name.Contains('/', StringComparison.Ordinal)
        && !name.Contains('\\', StringComparison.Ordinal)
        && !name.Contains('?', StringComparison.Ordinal)
        && !name.Contains('#', StringComparison.Ordinal);
}

public sealed record AllowListedService(string ServiceId, string ServiceName);
