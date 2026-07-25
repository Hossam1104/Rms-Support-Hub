using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Modules;

public class GhcEcommerceModule : IOrderModule
{
    public string Key => "ghc_ecommerce";
    public string Label => "GHC E-Commerce";
    public string Client => "GHC";
    public bool Available => true;

    // Real credentials are never hardcoded here. The connection string for this
    // module is resolved at request time via IConfiguration.GetConnectionString("GhcEcommerce"),
    // sourced from .NET user-secrets (dev) or environment variables (prod). See README.md.

    public IReadOnlyDictionary<string, ModuleEnvironment> Environments { get; } = new Dictionary<string, ModuleEnvironment>
    {
        ["GHC Production"] = new ModuleEnvironment
        {
            Key = "GHC Production",
            Environment = "Production",
            Description = "GHC live routing.",
            Accent = "ember",
            Cue = "Warehouse",
            Icon = "bi-box-seam",
            RouteLabel = "Live lane",
            VisualUrl = "static/assets/whites_logo.svg",
            VisualAlt = "GHC logo",
            Available = true,
            ApiUrl = "https://10.10.20.200/Gateway/RmsMainServerApi/api/Order/CreateAndAssignOrder",
            CancelUrl = "https://10.10.20.200/Gateway/RmsMainServerApi/api/Order/CancelOrder"
        },
        ["GHC Testing"] = new ModuleEnvironment
        {
            Key = "GHC Testing",
            Environment = "Testing",
            Description = "GHC QA routing.",
            Accent = "ocean",
            Cue = "Dispatch",
            Icon = "bi-truck",
            RouteLabel = "QA lane",
            VisualUrl = "static/assets/whites_logo.svg",
            VisualAlt = "GHC logo",
            Available = true,
            ApiUrl = "http://10.10.20.126:8090/RmsMainServerApi/api/Order/CreateAndAssignOrder",
            CancelUrl = "http://10.10.20.126:8090/RmsMainServerApi/api/Order/CancelOrder"
        }
    };

    public ModuleEnvironment GetEnvironment(string? envKey)
    {
        if (!string.IsNullOrEmpty(envKey) && Environments.TryGetValue(envKey, out var env))
            return env;
        return Environments.Values.First(e => e.Available);
    }

    public OrderDraft DefaultState()
    {
        return new OrderDraft
        {
            OrderData = new Dictionary<string, object?>
            {
                ["branch_code"] = "",
                ["order_code"] = "",
                ["parent_order_code"] = "",
                ["order_delivery_cost"] = 0m,
                ["is_delivery"] = true,
                ["order_status"] = "1",
                ["order_payment_status"] = "1",
                ["delivery_date"] = "",
                ["delivery_from_time"] = "",
                ["delivery_to_time"] = "",
                ["shipping_address_2"] = "",
                ["fullfilment_plant"] = "",
                ["order_notes"] = "",
                ["client_name"] = "",
                ["client_code"] = "",
                ["client_mobile"] = "",
                ["client_national_id"] = "",
                ["shipping_address"] = "",
                ["district_name"] = "",
                ["city_name"] = ""
            }
        };
    }

    public Dictionary<string, object?> BuildPayload(OrderDraft draft)
    {
        // Will delegate to FlatOrderPayloadBuilder in Session 4
        return new Dictionary<string, object?>();
    }

    public List<string> Validate(Dictionary<string, object?> payload)
    {
        // Will delegate to FlatOrderValidator in Session 4
        return new List<string>();
    }
}
