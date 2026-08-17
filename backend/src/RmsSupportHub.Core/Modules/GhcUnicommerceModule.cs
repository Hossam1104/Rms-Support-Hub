using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Services;

namespace RmsSupportHub.Core.Modules;

public class GhcUnicommerceModule : IOrderModule
{
    private readonly IUniCommercePayloadBuilder _payloadBuilder;
    private readonly IUniCommerceValidator _validator;

    public GhcUnicommerceModule(
        IUniCommercePayloadBuilder payloadBuilder,
        IUniCommerceValidator validator,
        IReadOnlyDictionary<string, ModuleEnvironment>? environments = null)
    {
        _payloadBuilder = payloadBuilder;
        _validator = validator;
        Environments = environments ?? ModuleEnvironmentDefaults.GhcUnicommerce();
    }

    public string Key => "ghc_unicommerce";
    public string Label => "GHC Uni-Commerce";
    public string Client => "GHC";
    public bool Available => true;

    /// <summary>No item/consumer repository was ever built for Uni-Commerce,
    /// and its environments have no ApiUrl yet, so Cancel/Resend/OrderRequests
    /// stay false until that lands.</summary>
    public ModuleCapabilities Capabilities { get; } = new(
        DraftKind: "unicommerce",
        ItemLookup: false,
        ConsumerLookup: false,
        OrderRequests: false,
        Cancel: false,
        Resend: false);

    public IReadOnlyDictionary<string, ModuleEnvironment> Environments { get; }

    public ModuleEnvironment GetEnvironment(string? envKey) => ModuleEnvironmentResolver.Resolve(Environments, envKey);

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

    /// <summary>Maps the generic OrderDraft.OrderData bag into the typed
    /// UniCommerceInvoice the payload builder expects. Ported from
    /// OrderController.BuildPayloadForModule's former "ghc_unicommerce" switch
    /// arm (see remediation_plan.md B21) so the module is self-sufficient and
    /// the controller no longer needs to know this module's draft shape.</summary>
    public Dictionary<string, object?> BuildPayload(OrderDraft draft)
    {
        var data = draft.OrderData;
        var invoice = new UniCommerceInvoice
        {
            ReferenceNumber = data.GetValueOrDefault("reference_number")?.ToString() ?? "",
            OnlineOrderNumber = data.GetValueOrDefault("online_order_number")?.ToString() ?? "",
            IsReturn = data.TryGetValue("is_return", out var r) && r is true,
            ParentReferenceNumber = data.GetValueOrDefault("parent_reference_number")?.ToString(),
            CustomerName = data.GetValueOrDefault("customer_name")?.ToString() ?? "AMAZON",
            PaidOnlineAmount = decimal.TryParse(data.GetValueOrDefault("paid_online_amount")?.ToString(), out var po) ? po : 0m,
            PaidWithPointsAmount = decimal.TryParse(data.GetValueOrDefault("paid_with_points_amount")?.ToString(), out var pp) ? pp : 0m,
            Consumer = draft.Consumer,
            Delivery = draft.Delivery,
            RowItems = draft.RowItems
        };
        return _payloadBuilder.BuildInvoicePayload(invoice);
    }

    public List<string> Validate(OrderDraft draft)
    {
        var payload = BuildPayload(draft);
        return _validator.ValidatePayload(payload);
    }

    public Task<Product?> LookupItemAsync(string connectionString, string code, string? branchCode = null) =>
        throw new NotSupportedException("Item lookup is not available for GHC Uni-Commerce.");

    public Task<Consumer?> LookupConsumerByPhoneAsync(string connectionString, string phone) =>
        throw new NotSupportedException("Consumer lookup is not available for GHC Uni-Commerce.");
}
