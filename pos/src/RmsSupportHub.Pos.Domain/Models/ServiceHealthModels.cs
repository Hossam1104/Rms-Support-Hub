using System.Security.Cryptography;
using System.Text;
using RmsSupportHub.Pos.Domain.Enums;

namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>
/// Fixed service identities used by the read-only local service-health projection. The set is
/// source-owned and cannot be replaced by a caller or installed configuration.
/// </summary>
public sealed record ServiceHealthDefinition(
    string ServiceName,
    string DisplayName,
    bool Required);

public static class ServiceIdentityCatalog
{
    public static string ToServiceId(string serviceName) =>
        "svc-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serviceName.Trim())))
            .ToLowerInvariant()[..16];

    public static bool IsOpaqueServiceId(string? serviceId) =>
        serviceId is { Length: 20 }
        && serviceId.StartsWith("svc-", StringComparison.Ordinal)
        && serviceId[4..].All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public static class ServiceHealthCatalog
{
    public static IReadOnlyList<ServiceHealthDefinition> Definitions { get; } =
        RmsServiceCatalog.Definitions
            .Select(definition => new ServiceHealthDefinition(
                definition.ServiceName,
                definition.DisplayName,
                Required: true))
            .Append(new ServiceHealthDefinition(
                AgentProductIdentity.PermanentServiceName,
                AgentProductIdentity.ServiceDisplayName,
                Required: true))
            .ToArray();
}

public enum ServiceHealthOverallState
{
    Healthy,
    Degraded,
    Unknown
}

/// <summary>Sanitized status for one fixed service identity.</summary>
public sealed record ServiceHealthItem(
    string ServiceName,
    string ServiceId,
    string DisplayName,
    bool Required,
    ServiceStatus RuntimeState,
    string SafeStatusCode)
{
    public bool CanControl => RmsServiceCatalog.Definitions.Any(definition =>
        string.Equals(definition.ServiceName, ServiceName, StringComparison.Ordinal));
}

public sealed record ServiceHealthSnapshot(
    ServiceHealthOverallState OverallState,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<ServiceHealthItem> Services);
