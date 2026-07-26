using OnlineOrderTool.Core.Models;
using OnlineOrderTool.Core.Repositories;
using OnlineOrderTool.Core.Services;

namespace OnlineOrderTool.Core.Modules;

public class GhcEcommerceModule : IOrderModule
{
    private readonly IFlatOrderPayloadBuilder _payloadBuilder;
    private readonly IFlatOrderValidator _validator;
    private readonly IItemRepository _itemRepository;
    private readonly IConsumerRepository _consumerRepository;

    public GhcEcommerceModule(
        IFlatOrderPayloadBuilder payloadBuilder,
        IFlatOrderValidator validator,
        IItemRepository itemRepository,
        IConsumerRepository consumerRepository)
    {
        _payloadBuilder = payloadBuilder;
        _validator = validator;
        _itemRepository = itemRepository;
        _consumerRepository = consumerRepository;
    }

    public string Key => "ghc_ecommerce";
    public string Label => "GHC E-Commerce";
    public string Client => "GHC";
    public bool Available => true;

    /// <summary>OrderRequests is false pending confirmed GHC database
    /// credentials -- flip to true once GHC's OrderRequests table has been
    /// verified live the same way UPC's was (see docs/database-schema.md).
    /// That one flip is all OrderRequestsController needs; nothing else
    /// keys off module identity. // TODO(db-creds)</summary>
    public ModuleCapabilities Capabilities { get; } = new(
        DraftKind: "flat",
        ItemLookup: true,
        ConsumerLookup: true,
        OrderRequests: false,
        Cancel: true,
        Resend: true,
        HasDeliveryFields: true);

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
            CancelUrl = "https://10.10.20.200/Gateway/RmsMainServerApi/api/Order/CancelOrder",
            ConnectionStringName = "GhcEcommerce"
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
            IsDefault = true,
            ApiUrl = "http://10.10.20.126:8090/RmsMainServerApi/api/Order/CreateAndAssignOrder",
            CancelUrl = "http://10.10.20.126:8090/RmsMainServerApi/api/Order/CancelOrder",
            ConnectionStringName = "GhcEcommerce"
        }
    };

    public ModuleEnvironment GetEnvironment(string? envKey) => ModuleEnvironmentResolver.Resolve(Environments, envKey);

    /// <summary>Keys match the corrected contract FlatOrderPayloadBuilder reads
    /// (see R1) -- not the pre-R1 invented client_name/client_code/client_mobile/
    /// shipping_address/district_name/city_name, which the builder no longer
    /// reads at all. order_payment_status is intentionally not seeded here:
    /// build_payload always computes it from the payments list, it never reads
    /// it from OrderData. Default values otherwise mirror get_default_data()
    /// in _legacy_flask/config.py.</summary>
    public OrderDraft DefaultState()
    {
        return new OrderDraft
        {
            OrderData = new Dictionary<string, object?>
            {
                ["branch_code"] = "2000",
                ["order_code"] = $"ORD{DateTime.UtcNow:yyyyMMddHHmmss}",
                ["parent_order_code"] = "",
                ["order_delivery_cost"] = 10.0m,
                ["is_delivery"] = true,
                ["order_status"] = "new",
                ["delivery_date"] = "",
                ["delivery_from_time"] = "",
                ["delivery_to_time"] = "",
                ["shipping_address_2"] = "",
                ["fullfilment_plant"] = "MAIN",
                ["order_notes"] = "Don't Ring the bell",
                ["client_country_code"] = "966",
                ["client_phone"] = "",
                ["client_first_name"] = "",
                ["client_middle_name"] = "",
                ["client_last_name"] = "",
                ["client_email"] = "",
                ["client_birthdate"] = "",
                ["client_gender"] = "Male",
                ["order_address"] = "",
                ["address_code"] = "",
                ["order_country_code"] = "",
                ["order_phone"] = ""
            }
        };
    }

    public Dictionary<string, object?> BuildPayload(OrderDraft draft) =>
        _payloadBuilder.BuildPayload(draft, FlatVariant.GhcVariant);

    public List<string> Validate(OrderDraft draft)
    {
        var payload = BuildPayload(draft);
        var totalPaid = TotalsCalculator.Calculate(draft).TotalPaidAmount;
        return _validator.ValidatePayload(payload, FlatVariant.GhcVariant, totalPaid);
    }

    public Task<Product?> LookupItemAsync(string connectionString, string code, string? branchCode = null) =>
        _itemRepository.LookupItemAsync(connectionString, code, branchCode);

    public Task<Consumer?> LookupConsumerByPhoneAsync(string connectionString, string phone) =>
        _consumerRepository.LookupConsumerByPhoneAsync(connectionString, phone);
}
