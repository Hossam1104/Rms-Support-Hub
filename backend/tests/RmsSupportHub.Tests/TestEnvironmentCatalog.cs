using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Modules;

namespace RmsSupportHub.Tests;

/// <summary>Non-secret server-owned environment registrations used by direct
/// controller/service tests. Production is present only to verify that the
/// Testing policy rejects it before a repository or downstream client runs.</summary>
internal static class TestEnvironmentCatalog
{
    public static IReadOnlyDictionary<string, ModuleEnvironment> Upc() =>
        ModuleEnvironmentDefaults.UpcEcommerce()
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value with
                {
                    Available = true,
                    ApiUrl = entry.Key == "UPC Production"
                        ? "http://10.10.10.181/RmsMainServerApi/api/Order/CreateAndAssignOrder"
                        : "http://10.10.10.181:8080/RmsMainServerApi/api/Order/CreateAndAssignOrder",
                    CancelUrl = entry.Key == "UPC Production"
                        ? "http://10.10.10.181/RmsMainServerApi/api/Order/CancelOrder"
                        : "http://10.10.10.181:8080/RmsMainServerApi/api/Order/CancelOrder",
                    ConnectionStringName = "UpcEcommerceTest",
                    DatabaseOverride = entry.Key == "UPC Production" ? "RmsMainProd" : null,
                    HealthProbeEnabled = true
                },
                StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ModuleEnvironment>> UpcOnly() =>
        new Dictionary<string, IReadOnlyDictionary<string, ModuleEnvironment>>(StringComparer.OrdinalIgnoreCase)
        {
            ["upc_ecommerce"] = Upc()
        };

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ModuleEnvironment>> UpcAndGhcUni() =>
        new Dictionary<string, IReadOnlyDictionary<string, ModuleEnvironment>>(StringComparer.OrdinalIgnoreCase)
        {
            ["upc_ecommerce"] = Upc(),
            ["ghc_unicommerce"] = ModuleEnvironmentDefaults.GhcUnicommerce()
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value with
                    {
                        Available = true,
                        ApiUrl = "http://uni.example/create",
                        ConnectionStringName = "GhcUnicommerceTest",
                        HealthProbeEnabled = true
                    },
                    StringComparer.OrdinalIgnoreCase)
        };
}
