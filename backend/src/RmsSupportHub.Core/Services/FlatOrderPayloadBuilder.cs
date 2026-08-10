using System.Globalization;
using System.Text.Json;
using RmsSupportHub.Core.Models;

namespace RmsSupportHub.Core.Services;

/// <summary>
/// The GHC and UPC flat-order APIs share one JSON schema; only a handful of
/// fields differ. See docs/request_examples/GHC E-Commerce/request_body.json
/// and docs/request_examples/UPC/4- ….json — GhcVariant/UpcVariant encode
/// exactly the observed differences between those two reference payloads.
/// </summary>
public sealed record FlatVariant(
    bool IncludeOrderContactFields,
    bool IncludeDeliveryFields,
    bool IncludeCreditInfo,
    bool IncludeCardBankInfo,
    bool IncludePaymentStatus,
    IReadOnlyList<string> AllowedPaymentMethods)
{
    /// <summary>Every method the GHC flat-order API accepts on a payment row.</summary>
    public static readonly IReadOnlyList<string> GhcPaymentMethods = new[]
    {
        "COD", "Visa", "RajhiPoints", "Tamara", "Tabby", "NeqatyPoints",
        "QitafPoints", "MisPay", "Emkan", "YouGotaGift", "OgMoney", "PostToCredit"
    };

    /// <summary>UPC only settles through these three providers. Cash on delivery
    /// is not an explicit method there: a UPC order with no payment rows is the
    /// COD shape (order_payment_method COD / order_payment_status not_payment),
    /// so a "COD" row is a malformed payload rather than a cash order.</summary>
    public static readonly IReadOnlyList<string> UpcPaymentMethods = new[] { "Visa", "Tamara", "Tabby" };

    /// <summary>order_country_code/order_phone, delivery_date/from/to,
    /// shipping_address_2, fullfilment_plant, card_name/bank_code and a nested
    /// credit_customer_info are present in the GHC reference; payment_status is not.</summary>
    public static readonly FlatVariant GhcVariant = new(
        IncludeOrderContactFields: true,
        IncludeDeliveryFields: true,
        IncludeCreditInfo: true,
        IncludeCardBankInfo: true,
        IncludePaymentStatus: false,
        AllowedPaymentMethods: GhcPaymentMethods);

    /// <summary>UPC's reference payload has none of the GHC-only fields above,
    /// but does include payment_status on every payment.</summary>
    public static readonly FlatVariant UpcVariant = new(
        IncludeOrderContactFields: false,
        IncludeDeliveryFields: false,
        IncludeCreditInfo: false,
        IncludeCardBankInfo: false,
        IncludePaymentStatus: true,
        AllowedPaymentMethods: UpcPaymentMethods);
}

public interface IFlatOrderPayloadBuilder
{
    Dictionary<string, object?> BuildPayload(OrderDraft draft, FlatVariant variant);
}

public class FlatOrderPayloadBuilder : IFlatOrderPayloadBuilder
{
    private static readonly string[] CreditMethods = { "PostToCredit" };
    private static readonly string[] DigitalWallets = { "Tamara", "Tabby", "MisPay", "Emkan", "Visa" };
    private static readonly string[] PointsMethods = { "RajhiPoints", "NeqatyPoints", "QitafPoints", "Points" };
    private static readonly double[] DefaultGps = { 21.779006345949554, 39.08578576461103 };

    public static decimal NormalizeVatPercentage(decimal rawVat)
    {
        if (rawVat <= 0m) return 0m;
        if (rawVat > 0m && rawVat <= 1m) return Math.Round(rawVat * 100m, 2);
        return Math.Round(rawVat, 2);
    }

    public static Dictionary<string, object?> FormatProduct(Product product)
    {
        var vatPct = NormalizeVatPercentage(product.VatPercentage);
        var vatDecimal = Math.Round(vatPct / 100m, 4);

        return new Dictionary<string, object?>
        {
            ["item_code"] = product.ItemCode ?? "",
            ["item_name"] = product.ItemName ?? "",
            ["quantity"] = product.Quantity,
            ["unit_price"] = product.UnitPrice,
            ["offer_code"] = product.OfferCode ?? "",
            ["offer_message"] = product.OfferMessage ?? "",
            ["row_total_discount"] = product.Discount,
            ["unit_vat_amount"] = product.UnitVat,
            ["total_vat_amount"] = product.TotalVat,
            ["vat_percentage"] = vatDecimal,
            ["row_net_total"] = product.EstimatedTotal
        };
    }

    /// <summary>Ported from _prepare_payments / build_payload in
    /// _legacy_flask/modules/flat_order.py, extended per variant with
    /// card_name/bank_code (GHC) and payment_status (UPC).</summary>
    public static Dictionary<string, object?> FormatPayment(Payment payment, FlatVariant variant)
    {
        var dict = new Dictionary<string, object?>
        {
            ["payment_method"] = payment.PaymentMethod ?? ""
        };

        if (variant.IncludePaymentStatus)
        {
            dict["payment_status"] = payment.PaymentStatus ?? "";
        }

        dict["payment_amount"] = Math.Round(payment.PaymentAmount, 2);
        dict["transaction_id"] = payment.TransactionId ?? "";
        dict["payment_option"] = payment.PaymentOption ?? "";

        if (variant.IncludeCardBankInfo)
        {
            dict["card_name"] = payment.CardName ?? "";
            dict["bank_code"] = payment.BankCode ?? "";
        }

        dict["option_commission"] = Math.Round(payment.OptionCommission, 2);

        if (variant.IncludeCreditInfo)
        {
            dict["credit_customer_info"] = payment.PaymentMethod == "PostToCredit"
                ? new Dictionary<string, object?>
                {
                    ["customer_number"] = payment.CustomerNumber ?? "",
                    ["customer_name"] = payment.CustomerName ?? ""
                }
                : null;
        }

        return dict;
    }

    /// <summary>Turns a flat-order draft into the exact JSON the GHC/UPC
    /// CreateAndAssignOrder API expects. Ported from build_payload /
    /// build_upc_payload in _legacy_flask/modules/flat_order.py, which were
    /// ~95% identical; variant encodes the real differences (see FlatVariant).</summary>
    public Dictionary<string, object?> BuildPayload(OrderDraft draft, FlatVariant variant)
    {
        var data = draft.OrderData;
        var products = draft.Products.Select(FormatProduct).ToList();
        var payments = draft.Payments.Select(p => FormatPayment(p, variant)).ToList();

        var orderProductTotal = Math.Round(draft.Products.Sum(p => p.EstimatedTotal), 2);
        var orderDiscount = Math.Round(draft.Products.Sum(p => p.Discount), 2);
        var deliveryCost = Math.Round(GetDecimal(data, "order_delivery_cost"), 2);
        var orderFinalTotal = Math.Round(orderProductTotal + deliveryCost, 2);

        var payload = new Dictionary<string, object?>
        {
            ["branch_code"] = GetString(data, "branch_code"),
            ["order_code"] = GetString(data, "order_code"),
            ["parent_order_code"] = GetString(data, "parent_order_code"),
            ["order_creation_date"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            ["order_notes"] = GetString(data, "order_notes", "Don't Ring the bell"),
            ["order_product_total_value"] = orderProductTotal,
            ["is_delivery"] = GetBool(data, "is_delivery", true) ? 1 : 0,
            ["order_delivery_cost"] = deliveryCost,
            ["order_total_discount"] = orderDiscount,
            ["order_final_total_value"] = orderFinalTotal,
            ["order_payment_method"] = GetPaymentMethodString(draft.Payments),
            ["order_status"] = GetString(data, "order_status", "new"),
            ["client_country_code"] = GetString(data, "client_country_code", "966"),
            // The country code travels in client_country_code, so the number
            // itself must never repeat it -- Normalizers.NormalizeLocalPhone
            // is the authoritative boundary for that split and also covers
            // drafts saved before the rule existed.
            ["client_phone"] = Normalizers.NormalizeLocalPhone(GetString(data, "client_phone")),
            ["client_first_name"] = GetString(data, "client_first_name"),
            ["client_middle_name"] = GetString(data, "client_middle_name"),
            ["client_last_name"] = GetString(data, "client_last_name"),
            ["client_email"] = GetString(data, "client_email"),
            ["client_birthdate"] = FormatBirthdate(GetString(data, "client_birthdate")),
            ["client_gender"] = GetString(data, "client_gender", "Male"),
            ["order_address"] = GetString(data, "order_address"),
            ["address_code"] = GetString(data, "address_code"),
            ["order_payment_status"] = DeterminePaymentStatus(draft.Payments, orderFinalTotal),
            ["order_gps"] = GetGps(data, "order_gps"),
            ["order_products"] = products,
            ["payment_methods_with_options"] = payments
        };

        if (variant.IncludeOrderContactFields)
        {
            payload["order_country_code"] = GetString(data, "order_country_code");
            payload["order_phone"] = Normalizers.NormalizeLocalPhone(GetString(data, "order_phone"));
        }

        if (variant.IncludeDeliveryFields)
        {
            payload["delivery_date"] = GetString(data, "delivery_date");
            payload["delivery_from_time"] = FormatTime(GetString(data, "delivery_from_time"));
            payload["delivery_to_time"] = FormatTime(GetString(data, "delivery_to_time"));
            payload["shipping_address_2"] = GetString(data, "shipping_address_2");
            payload["fullfilment_plant"] = GetString(data, "fullfilment_plant");
        }

        return payload;
    }

    /// <summary>Ported from _format_birthdate in _legacy_flask/modules/flat_order.py.</summary>
    private static string FormatBirthdate(string birthdate)
    {
        if (string.IsNullOrEmpty(birthdate)) return "1989-04-11T12:00:00.000Z";
        if (birthdate.Contains('T')) return birthdate;
        if (birthdate.Length == 10) return $"{birthdate}T12:00:00.000Z";
        return birthdate;
    }

    /// <summary>Ported from _format_time in _legacy_flask/modules/flat_order.py.</summary>
    private static string FormatTime(string time)
    {
        if (string.IsNullOrEmpty(time)) return "";
        if (time.Length == 5) return time + ":00"; // HH:MM -> HH:MM:SS
        var dotIndex = time.IndexOf('.');
        return dotIndex >= 0 ? time[..dotIndex] : time; // HH:MM:SS.mmm -> HH:MM:SS
    }

    /// <summary>Ported from _get_payment_method_string in
    /// _legacy_flask/modules/flat_order.py: a comma-joined list of every
    /// payment's method, "COD" if there are none.</summary>
    private static string GetPaymentMethodString(List<Payment> payments)
    {
        var methods = payments
            .Select(p => p.PaymentMethod)
            .Where(m => !string.IsNullOrEmpty(m))
            .ToList();
        return methods.Count > 0 ? string.Join(",", methods) : "COD";
    }

    /// <summary>Ported line-for-line from _determine_payment_status in
    /// _legacy_flask/modules/flat_order.py, including the digital-wallet
    /// branch's fallthrough when no payment has status "done_payment" (it is
    /// not an early return in the legacy source, so it must not be one here).</summary>
    private static string DeterminePaymentStatus(List<Payment> payments, decimal orderFinalTotal)
    {
        if (payments.Count == 0) return "not_payment";

        var totalPaid = Math.Round(payments.Sum(p => p.PaymentAmount), 2);
        var methods = payments.Select(p => p.PaymentMethod ?? "").ToList();
        var statuses = payments.Select(p => p.PaymentStatus ?? "").ToList();

        if (methods.Any(m => CreditMethods.Contains(m))) return "not_payment";

        if (methods.Any(m => DigitalWallets.Contains(m)))
        {
            if (statuses.Contains("done_payment"))
            {
                return Math.Abs(totalPaid - orderFinalTotal) <= 0.01m ? "done_payment" : "partially_paid";
            }
            // No return here: legacy falls through to the remaining checks
            // when no digital-wallet payment has landed as "done_payment" yet.
        }

        if (methods.Contains("COD")) return "not_payment";

        if (methods.Any(m => PointsMethods.Contains(m)))
        {
            return statuses.Contains("done_payment") && Math.Abs(totalPaid - orderFinalTotal) <= 0.01m
                ? "done_payment"
                : "partially_paid";
        }

        if (methods.Count > 1) return "partially_paid";

        return statuses.Count > 0 ? statuses[0] : "not_payment";
    }

    /// <summary>order_gps default matches _legacy_flask's build_payload default
    /// exactly. Handles both native numeric collections (e.g. from
    /// DefaultState()) and JsonElement arrays (values that arrived via a
    /// deserialized HTTP body, e.g. PATCH order-field).</summary>
    private static List<double> GetGps(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var val) || val == null) return DefaultGps.ToList();

        var result = new List<double>();

        if (val is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in je.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out var d))
                    result.Add(d);
                else if (double.TryParse(item.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dd))
                    result.Add(dd);
            }
        }
        else if (val is System.Collections.IEnumerable enumerable && val is not string)
        {
            foreach (var item in enumerable)
            {
                if (item != null && double.TryParse(item.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dd))
                    result.Add(dd);
            }
        }

        return result.Count > 0 ? result : DefaultGps.ToList();
    }

    private static string GetString(Dictionary<string, object?> dict, string key, string defaultValue = "")
    {
        return dict.TryGetValue(key, out var val) && val != null ? val.ToString()! : defaultValue;
    }

    private static decimal GetDecimal(Dictionary<string, object?> dict, string key, decimal defaultValue = 0m)
    {
        if (!dict.TryGetValue(key, out var val) || val == null) return defaultValue;
        if (val is decimal d) return d;
        if (decimal.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return defaultValue;
    }

    private static bool GetBool(Dictionary<string, object?> dict, string key, bool defaultValue = false)
    {
        if (!dict.TryGetValue(key, out var val) || val == null) return defaultValue;
        if (val is bool b) return b;
        if (bool.TryParse(val.ToString(), out var parsed)) return parsed;
        return defaultValue;
    }
}
