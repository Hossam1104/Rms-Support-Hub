namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>
/// The only RMS Windows services that may cross the Agent's service-control boundary. This list is
/// source-owned and deliberately does not come from an installed JSON file.
/// </summary>
public sealed record RmsServiceDefinition(string ServiceName, string DisplayName);

public static class RmsServiceCatalog
{
    public const string BranchServiceName = "RMS.BranchService";
    public const string CashierServiceName = "RMS.CashierService";
    public const string ServicesManagerServiceName = "RMSServiceManager";

    public static IReadOnlyList<RmsServiceDefinition> Definitions { get; } =
    [
        new(BranchServiceName, "RMS Branch Service"),
        new(CashierServiceName, "RMS Cashier Service"),
        new(ServicesManagerServiceName, "RMS Services Manager")
    ];

    public static bool TryResolveServiceId(string? serviceId, out RmsServiceDefinition? definition)
    {
        definition = null;
        if (!ServiceIdentityCatalog.IsOpaqueServiceId(serviceId))
        {
            return false;
        }

        definition = Definitions.FirstOrDefault(item =>
            string.Equals(ServiceIdentityCatalog.ToServiceId(item.ServiceName), serviceId, StringComparison.Ordinal));
        return definition is not null;
    }

    public static bool IsAgentServiceId(string? serviceId) =>
        string.Equals(
            ServiceIdentityCatalog.ToServiceId(AgentProductIdentity.PermanentServiceName),
            serviceId,
            StringComparison.Ordinal);
}
