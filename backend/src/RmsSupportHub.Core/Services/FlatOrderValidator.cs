using System.Globalization;

namespace RmsSupportHub.Core.Services;

public interface IFlatOrderValidator
{
    /// <summary>
    /// Validates a built flat-order payload (see FlatOrderPayloadBuilder).
    /// totalPaid must be the REAL sum of payment amounts (e.g.
    /// TotalsCalculator.Calculate(draft).TotalPaidAmount) — the legacy Python
    /// validator compared against a "total_paid" payload key that was never
    /// emitted by build_payload, so its "must cover full amount" rules always
    /// compared against 0 and never correctly passed a real order. Passing the
    /// real total here is the fix; do not read total_paid from the payload.
    ///
    /// variant replaces a module-key string comparison (see
    /// remediation_plan.md B21): PostToCredit's rules key off
    /// variant.IncludeCreditInfo (true only for FlatVariant.GhcVariant)
    /// rather than comparing "moduleKey == ...".
    /// </summary>
    List<string> ValidatePayload(Dictionary<string, object?> payload, FlatVariant variant, decimal totalPaid);
}

public class FlatOrderValidator : IFlatOrderValidator
{
    private static readonly string[] DigitalWallets = { "Tamara", "Tabby", "MisPay", "Emkan", "Visa" };

    private static readonly string[] RequiredFields =
    {
        "branch_code", "order_code", "client_phone", "client_first_name", "order_address"
    };

    public List<string> ValidatePayload(Dictionary<string, object?> payload, FlatVariant variant, decimal totalPaid)
    {
        var errors = new List<string>();

        foreach (var field in RequiredFields)
        {
            if (!payload.TryGetValue(field, out var v) || string.IsNullOrWhiteSpace(v?.ToString()))
                errors.Add($"Missing required field: {field}");
        }

        var products = payload.GetValueOrDefault("order_products") as List<Dictionary<string, object?>>;
        if (products is null || products.Count == 0)
            errors.Add("Order must contain at least one product.");

        if (variant.RequireProductTotalAlignment && products is not null)
        {
            var compiledProductTotal = GetDecimal(payload, "order_product_total_value");
            var rowProductTotal = Math.Round(products.Sum(row => GetDecimal(row, "row_net_total")), 2);
            if (Math.Abs(compiledProductTotal - rowProductTotal) > 0.01m)
            {
                errors.Add($"order_product_total_value ({compiledProductTotal}) must equal the sum of order_products.row_net_total ({rowProductTotal}).");
            }
        }

        if (variant.IncludeDeliveryFields && GetDecimal(payload, "is_delivery") == 1m)
        {
            foreach (var field in new[] { "delivery_date", "delivery_from_time", "delivery_to_time" })
            {
                if (!payload.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value?.ToString()))
                    errors.Add($"Missing required field for delivery order: {field}");
            }
        }

        // An order with no payment rows is NOT an error: it is the verified
        // Cash-on-Delivery shape. FlatOrderPayloadBuilder already emits exactly
        // what the reference COD payload holds for that state --
        // order_payment_method "COD", order_payment_status "not_payment" and an
        // empty payment_methods_with_options (see
        // docs/request_examples/UPC/1- ….json, whose payment list is entirely
        // commented out). The per-payment rules below simply have nothing to
        // iterate, and no synthetic zero-value payment is fabricated to stand
        // in for one.
        var payments = payload.TryGetValue("payment_methods_with_options", out var paysObj)
            && paysObj is List<Dictionary<string, object?>> parsedPayments
                ? parsedPayments
                : new List<Dictionary<string, object?>>();

        var orderFinalTotal = GetDecimal(payload, "order_final_total_value");
        // order_payment_method is a comma-joined string (see
        // FlatOrderPayloadBuilder.GetPaymentMethodString) -- substring
        // containment here mirrors legacy's `method in payment_method` checks,
        // which operate on that same joined string.
        var paymentMethodString = payload.GetValueOrDefault("order_payment_method")?.ToString() ?? "";

        foreach (var p in payments)
        {
            var method = p.GetValueOrDefault("payment_method")?.ToString() ?? "";

            // The accepted method set is a property of the variant, not of the
            // module key: GHC keeps the full legacy list while UPC settles only
            // through Visa/Tamara/Tabby. A UPC cash order carries no payment row
            // at all, so an explicit "COD" row is rejected here like any other
            // method the variant does not accept.
            if (!string.IsNullOrEmpty(method) && !variant.AllowedPaymentMethods.Contains(method))
            {
                errors.Add($"Payment method '{method}' is not allowed. The allowed payment methods: [{string.Join(", ", variant.AllowedPaymentMethods)}]");
            }

            if (method == "PostToCredit" && !variant.IncludeCreditInfo)
            {
                errors.Add("PostToCredit payment method is not allowed for this module.");
            }

            // payment_status is only present on UPC payments (FlatVariant.UpcVariant) --
            // GHC's reference payload has no such field, so these status-shaped
            // checks are skipped for GHC rather than treated as a missing/blank status.
            if (p.TryGetValue("payment_status", out var statusRaw))
            {
                var status = statusRaw?.ToString() ?? "";

                if (method == "COD" && status != "not_payment")
                    errors.Add("COD payments must have 'not_payment' status.");

                if (DigitalWallets.Contains(method) && status != "done_payment")
                    errors.Add($"{method} payment status must be 'done_payment'.");

                if (method == "PostToCredit" && status != "not_payment")
                    errors.Add("PostToCredit payments must have 'not_payment' status.");
            }

            if (method == "PostToCredit" && variant.IncludeCreditInfo)
            {
                var creditInfo = p.GetValueOrDefault("credit_customer_info") as Dictionary<string, object?>;
                var custName = creditInfo?.GetValueOrDefault("customer_name")?.ToString();
                var custNumber = creditInfo?.GetValueOrDefault("customer_number")?.ToString();
                if (string.IsNullOrWhiteSpace(custName) || string.IsNullOrWhiteSpace(custNumber))
                    errors.Add("PostToCredit requires customer_name and customer_number in credit_customer_info.");
            }
        }

        // "Must cover full amount" rules, ported from legacy's validate() but
        // fixed to compare against the real totalPaid parameter instead of an
        // always-zero payload["total_paid"] (see the interface doc comment).
        if (paymentMethodString.Contains("PostToCredit") && Math.Abs(totalPaid - orderFinalTotal) > 0.01m)
        {
            errors.Add($"Credit payments must cover full order amount. Current: {totalPaid}, Required: {orderFinalTotal}");
        }

        if (DigitalWallets.Any(m => paymentMethodString.Contains(m))
            && (payload.GetValueOrDefault("order_payment_status")?.ToString() == "done_payment")
            && Math.Abs(totalPaid - orderFinalTotal) > 0.01m)
        {
            errors.Add($"Digital wallet 'done_payment' must equal order total. Current: {totalPaid}, Required: {orderFinalTotal}");
        }

        var tamaraCount = payments.Count(p => p.GetValueOrDefault("payment_method")?.ToString() == "Tamara");
        var tabbyCount = payments.Count(p => p.GetValueOrDefault("payment_method")?.ToString() == "Tabby");
        if (tamaraCount > 1) errors.Add("Only one Tamara payment method is allowed.");
        if (tabbyCount > 1) errors.Add("Only one Tabby payment method is allowed.");

        return errors;
    }

    private static decimal GetDecimal(Dictionary<string, object?> dict, string key)
    {
        if (dict.TryGetValue(key, out var val) && val != null
            && decimal.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }
        return 0m;
    }
}
