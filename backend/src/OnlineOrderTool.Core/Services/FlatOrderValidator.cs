using System.Globalization;

namespace OnlineOrderTool.Core.Services;

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
    /// </summary>
    List<string> ValidatePayload(Dictionary<string, object?> payload, string moduleKey, decimal totalPaid);
}

public class FlatOrderValidator : IFlatOrderValidator
{
    /// <summary>From config.PAYMENT_METHODS in _legacy_flask/config.py, plus
    /// PostToCredit (checked separately throughout flat_order.py but never
    /// listed in PAYMENT_METHODS itself).</summary>
    private static readonly string[] AllowedPaymentMethods =
    {
        "COD", "Visa", "RajhiPoints", "Tamara", "Tabby", "NeqatyPoints",
        "QitafPoints", "MisPay", "Emkan", "YouGotaGift", "OgMoney", "PostToCredit"
    };

    private static readonly string[] DigitalWallets = { "Tamara", "Tabby", "MisPay", "Emkan", "Visa" };

    private static readonly string[] RequiredFields =
    {
        "branch_code", "order_code", "client_phone", "client_first_name", "order_address"
    };

    public List<string> ValidatePayload(Dictionary<string, object?> payload, string moduleKey, decimal totalPaid)
    {
        var errors = new List<string>();

        foreach (var field in RequiredFields)
        {
            if (!payload.TryGetValue(field, out var v) || string.IsNullOrWhiteSpace(v?.ToString()))
                errors.Add($"Missing required field: {field}");
        }

        if (!payload.TryGetValue("order_products", out var prodsObj) || prodsObj is not List<Dictionary<string, object?>> prods || prods.Count == 0)
            errors.Add("Order must contain at least one product.");

        if (!payload.TryGetValue("payment_methods_with_options", out var paysObj) || paysObj is not List<Dictionary<string, object?>> payments || payments.Count == 0)
        {
            errors.Add("Order must contain at least one payment method.");
            return errors;
        }

        var orderFinalTotal = GetDecimal(payload, "order_final_total_value");
        // order_payment_method is a comma-joined string (see
        // FlatOrderPayloadBuilder.GetPaymentMethodString) -- substring
        // containment here mirrors legacy's `method in payment_method` checks,
        // which operate on that same joined string.
        var paymentMethodString = payload.GetValueOrDefault("order_payment_method")?.ToString() ?? "";

        foreach (var p in payments)
        {
            var method = p.GetValueOrDefault("payment_method")?.ToString() ?? "";

            if (!string.IsNullOrEmpty(method) && !AllowedPaymentMethods.Contains(method))
            {
                errors.Add($"Unknown payment method '{method}'. Use one of: {string.Join(", ", AllowedPaymentMethods)}.");
            }

            if (method == "PostToCredit" && moduleKey == "upc_ecommerce")
            {
                errors.Add("PostToCredit payment method is not allowed for UPC E-Commerce.");
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

            if (method == "PostToCredit" && moduleKey == "ghc_ecommerce")
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
