using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Repositories;
using RmsSupportHub.Core.Services;

namespace RmsSupportHub.Core.Modules;

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
        IConsumerRepository consumerRepository,
        IReadOnlyDictionary<string, ModuleEnvironment>? environments = null)
    {
        _payloadBuilder = payloadBuilder;
        _validator = validator;
        _itemRepository = itemRepository;
        _consumerRepository = consumerRepository;
        Environments = environments ?? ModuleEnvironmentDefaults.GhcEcommerce();
    }

    public string Key => "ghc_ecommerce";
    public string Label => "GHC E-Commerce";
    public string Client => "GHC";
    public bool Available => true;

    public ModuleCapabilities Capabilities { get; } = new(
        DraftKind: "flat",
        ItemLookup: true,
        ConsumerLookup: true,
        OrderRequests: true,
        Cancel: true,
        Resend: false,
        HasDeliveryFields: true);

    public IReadOnlyDictionary<string, ModuleEnvironment> Environments { get; }

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
