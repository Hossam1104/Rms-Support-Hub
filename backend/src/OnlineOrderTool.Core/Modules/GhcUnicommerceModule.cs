using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Modules;

public class GhcUnicommerceModule : IOrderModule
{
    public string Key => "ghc_unicommerce";
    public string Label => "GHC Uni-Commerce";
    public string Client => "GHC";
    public bool Available => true;

    // Real credentials are never hardcoded here. The connection string for this
    // module is resolved at request time via IConfiguration.GetConnectionString("GhcUnicommerce"),
    // sourced from .NET user-secrets (dev) or environment variables (prod). See README.md.

    public IReadOnlyDictionary<string, ModuleEnvironment> Environments { get; } = new Dictionary<string, ModuleEnvironment>
    {
        ["GHC Uni-Commerce Production"] = new ModuleEnvironment
        {
            Key = "GHC Uni-Commerce Production",
            Environment = "Production",
            Description = "GHC Uni-Commerce live routing (pending API URL).",
            Accent = "aurora",
            Cue = "Automation",
            Icon = "bi-cpu",
            RouteLabel = "Pending lane",
            VisualUrl = "static/assets/whites_logo.svg",
            VisualAlt = "GHC Uni-Commerce logo",
            Available = false,
            ApiUrl = null,
            CancelUrl = null
        },
        ["GHC Uni-Commerce Testing"] = new ModuleEnvironment
        {
            Key = "GHC Uni-Commerce Testing",
            Environment = "Testing",
            Description = "GHC Uni-Commerce QA routing (pending API URL).",
            Accent = "violet",
            Cue = "Staging",
            Icon = "bi-hourglass-split",
            RouteLabel = "Pending lane",
            VisualUrl = "static/assets/whites_logo.svg",
            VisualAlt = "GHC Uni-Commerce logo",
            Available = false,
            ApiUrl = null,
            CancelUrl = null
        }
    };

    public ModuleEnvironment GetEnvironment(string? envKey)
    {
        if (!string.IsNullOrEmpty(envKey) && Environments.TryGetValue(envKey, out var env))
            return env;
        return Environments.Values.First();
    }

    public OrderDraft DefaultState()
    {
        return new OrderDraft
        {
            OrderData = new Dictionary<string, object?>
            {
                ["reference_number"] = "",
                ["online_order_number"] = "",
                ["is_return"] = false,
                ["parent_reference_number"] = "",
                ["order_creation_date"] = "",
                ["customer_name"] = "AMAZON",
                ["paid_online_amount"] = 0m,
                ["paid_with_points_amount"] = 0m
            },
            Consumer = new Consumer(),
            Delivery = new DeliveryDetails(),
            RowItems = new List<RowItem>()
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
