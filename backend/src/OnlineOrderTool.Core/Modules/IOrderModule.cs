using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Modules;

/// <summary>What a module actually supports, so callers dispatch on data
/// instead of comparing module-key strings (see remediation_plan.md B21).
/// DraftKind selects which draft/payload shape a module uses ("flat" for
/// GHC/UPC E-Commerce, "unicommerce" for GHC Uni-Commerce, null for the
/// not-yet-available placeholders).</summary>
public record ModuleCapabilities(
    string? DraftKind,
    bool ItemLookup,
    bool ConsumerLookup,
    bool OrderRequests,
    bool Cancel,
    bool Resend,
    /// <summary>Mirrors FlatVariant.IncludeDeliveryFields (Services/FlatOrderPayloadBuilder.cs)
    /// -- true only for ghc_ecommerce. Lets the builder UI show/hide the
    /// Delivery card without comparing module-key strings (R10,
    /// remediation_plan.md B21).</summary>
    bool HasDeliveryFields = false,
    /// <summary>True when the module's database has a readable branch table
    /// backing GET /api/modules/{key}/branches (U3, UI_Rework_Plan.md D6/D7)
    /// -- true only for upc_ecommerce today; GHC stays false pending
    /// confirmed credentials/schema.</summary>
    bool BranchLookup = false
);

public interface IOrderModule
{
    string Key { get; }
    string Label { get; }
    string Client { get; }
    bool Available { get; }
    ModuleCapabilities Capabilities { get; }
    IReadOnlyDictionary<string, ModuleEnvironment> Environments { get; }

    ModuleEnvironment GetEnvironment(string? envKey);
    OrderDraft DefaultState();
    Dictionary<string, object?> BuildPayload(OrderDraft draft);

    /// <summary>Builds the payload from draft internally and validates it,
    /// so the caller never needs to know which validator or which totals
    /// calculation this module's draft kind requires.</summary>
    List<string> Validate(OrderDraft draft);

    /// <summary>Throws NotSupportedException if Capabilities.ItemLookup is
    /// false -- callers must check the capability first via CapabilityGuard;
    /// this is not itself a safety boundary.</summary>
    Task<Product?> LookupItemAsync(string connectionString, string code, string? branchCode = null);

    /// <summary>Throws NotSupportedException if Capabilities.ConsumerLookup
    /// is false -- see LookupItemAsync.</summary>
    Task<Consumer?> LookupConsumerByPhoneAsync(string connectionString, string phone);
}
