using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Modules;

public class UpcEcommerceModule : IOrderModule
{
    public string Key => "upc_ecommerce";
    public string Label => "UPC E-Commerce";
    public string Client => "UPC";
    public bool Available => true;

    // Real credentials are never hardcoded here. The connection string for each
    // environment is resolved at request time via IConfiguration.GetConnectionString(
    // "UpcEcommerceProd" | "UpcEcommerceTest"), sourced from .NET user-secrets (dev)
    // or environment variables (prod). See README.md.

    public IReadOnlyDictionary<string, ModuleEnvironment> Environments { get; } = new Dictionary<string, ModuleEnvironment>
    {
        ["UPC Production"] = new ModuleEnvironment
        {
            Key = "UPC Production",
            Environment = "Production",
            Description = "UPC live routing.",
            Accent = "sunrise",
            Cue = "Retail Ops",
            Icon = "bi-bag-check",
            RouteLabel = "Live lane",
            VisualUrl = "static/assets/upc_logo.svg",
            VisualAlt = "UPC logo",
            Available = true,
            ApiUrl = "http://10.10.10.181/RmsMainServerApi/api/Order/CreateAndAssignOrder",
            CancelUrl = "http://10.10.10.181/RmsMainServerApi/api/Order/CancelOrder"
        },
        ["UPC Testing"] = new ModuleEnvironment
        {
            Key = "UPC Testing",
            Environment = "Testing",
            Description = "UPC QA routing.",
            Accent = "electric",
            Cue = "QA Grid",
            Icon = "bi-sliders2-vertical",
            RouteLabel = "Test lane",
            VisualUrl = "static/assets/upc_logo.svg",
            VisualAlt = "UPC logo",
            Available = true,
            ApiUrl = "http://10.10.9.181:8080/RmsMainServerApi/api/Order/CreateAndAssignOrder",
            CancelUrl = "http://10.10.9.181:8080/RmsMainServerApi/api/Order/CancelOrder"
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
        return new Dictionary<string, object?>();
    }

    public List<string> Validate(Dictionary<string, object?> payload)
    {
        return new List<string>();
    }
}
