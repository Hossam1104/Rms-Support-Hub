using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Repositories;
using RmsSupportHub.Core.Services;

namespace RmsSupportHub.Core.Modules;

public class UpcEcommerceModule : IOrderModule
{
    private readonly IFlatOrderPayloadBuilder _payloadBuilder;
    private readonly IFlatOrderValidator _validator;
    private readonly IItemRepository _itemRepository;
    private readonly IConsumerRepository _consumerRepository;

    public UpcEcommerceModule(
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

    public string Key => "upc_ecommerce";
    public string Label => "UPC E-Commerce";
    public string Client => "UPC";
    public bool Available => true;

    public ModuleCapabilities Capabilities { get; } = new(
        DraftKind: "flat",
        ItemLookup: true,
        ConsumerLookup: true,
        OrderRequests: true,
        Cancel: true,
        Resend: true,
        HasDeliveryFields: false,
        BranchLookup: true);

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
            ApiUrl = "https://10.10.10.181/RmsMainServerApi/api/Order/CreateAndAssignOrder",
            CancelUrl = "https://10.10.10.181/RmsMainServerApi/api/Order/CancelOrder",
            ConnectionStringName = "UpcEcommerceProd"
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
            IsDefault = true,
            ApiUrl = "http://10.10.10.181:8080/RmsMainServerApi/api/Order/CreateAndAssignOrder",
            CancelUrl = "http://10.10.10.181:8080/RmsMainServerApi/api/Order/CancelOrder",
            ConnectionStringName = "UpcEcommerceTest"
        }
    };

    public ModuleEnvironment GetEnvironment(string? envKey) => ModuleEnvironmentResolver.Resolve(Environments, envKey);

    /// <summary>Keys match the corrected contract FlatOrderPayloadBuilder reads
    /// (see R1) -- not the pre-R1 invented client_name/client_code/client_mobile/
    /// shipping_address/district_name/city_name, which the builder no longer
    /// reads at all. order_payment_status is intentionally not seeded here:
    /// build_payload always computes it from the payments list, it never reads
    /// it from OrderData. UPC has no delivery/fulfillment fields, matching
    /// FlatVariant.UpcVariant. Default values otherwise mirror get_default_data()
    /// in _legacy_flask/config.py.</summary>
    public OrderDraft DefaultState()
    {
        return new OrderDraft
        {
            OrderData = new Dictionary<string, object?>
            {
                ["branch_code"] = "",
                ["order_code"] = $"ORD{DateTime.UtcNow:yyyyMMddHHmmss}",
                ["parent_order_code"] = "",
                ["order_delivery_cost"] = 10.0m,
                ["is_delivery"] = true,
                ["order_status"] = "new",
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
                ["address_code"] = ""
            }
        };
    }

    public Dictionary<string, object?> BuildPayload(OrderDraft draft) =>
        _payloadBuilder.BuildPayload(draft, FlatVariant.UpcVariant);

    public List<string> Validate(OrderDraft draft)
    {
        var payload = BuildPayload(draft);
        var totalPaid = TotalsCalculator.Calculate(draft).TotalPaidAmount;
        return _validator.ValidatePayload(payload, FlatVariant.UpcVariant, totalPaid);
    }

    public Task<Product?> LookupItemAsync(string connectionString, string code, string? branchCode = null) =>
        _itemRepository.LookupItemAsync(connectionString, code, branchCode);

    public Task<Consumer?> LookupConsumerByPhoneAsync(string connectionString, string phone) =>
        _consumerRepository.LookupConsumerByPhoneAsync(connectionString, phone);
}
