using System.Globalization;
using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Services;

public interface IFlatOrderPayloadBuilder
{
    Dictionary<string, object?> BuildGhcPayload(OrderDraft draft);
    Dictionary<string, object?> BuildUpcPayload(OrderDraft draft);
}

public class FlatOrderPayloadBuilder : IFlatOrderPayloadBuilder
{
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
            ["vat_percentage"] = vatDecimal,
            ["discount"] = product.Discount,
            ["offer_code"] = string.IsNullOrEmpty(product.OfferCode) ? null : product.OfferCode,
            ["offer_message"] = string.IsNullOrEmpty(product.OfferMessage) ? null : product.OfferMessage
        };
    }

    public static Dictionary<string, object?> FormatPayment(Payment payment)
    {
        var dict = new Dictionary<string, object?>
        {
            ["payment_method"] = payment.PaymentMethod ?? "",
            ["payment_status"] = payment.PaymentStatus ?? "",
            ["payment_amount"] = payment.PaymentAmount,
            ["transaction_id"] = string.IsNullOrEmpty(payment.TransactionId) ? null : payment.TransactionId,
            ["payment_option"] = string.IsNullOrEmpty(payment.PaymentOption) ? null : payment.PaymentOption,
            ["option_commission"] = payment.OptionCommission
        };

        if (payment.PaymentMethod == "PostToCredit")
        {
            dict["customer_name"] = payment.CustomerName ?? "";
            dict["customer_number"] = payment.CustomerNumber ?? "";
        }

        return dict;
    }

    public Dictionary<string, object?> BuildGhcPayload(OrderDraft draft)
    {
        var data = draft.OrderData;
        var products = draft.Products.Select(FormatProduct).ToList();
        var payments = draft.Payments.Select(FormatPayment).ToList();

        var payload = new Dictionary<string, object?>
        {
            ["branch_code"] = GetString(data, "branch_code"),
            ["order_code"] = GetString(data, "order_code"),
            ["parent_order_code"] = GetOptionalString(data, "parent_order_code"),
            ["order_delivery_cost"] = GetDecimal(data, "order_delivery_cost"),
            ["is_delivery"] = GetBool(data, "is_delivery", true),
            ["order_status"] = GetString(data, "order_status", "1"),
            ["order_payment_status"] = GetString(data, "order_payment_status", "1"),
            ["delivery_date"] = GetOptionalString(data, "delivery_date"),
            ["delivery_from_time"] = GetOptionalString(data, "delivery_from_time"),
            ["delivery_to_time"] = GetOptionalString(data, "delivery_to_time"),
            ["shipping_address_2"] = GetOptionalString(data, "shipping_address_2"),
            ["fullfilment_plant"] = GetOptionalString(data, "fullfilment_plant"),
            ["order_notes"] = GetOptionalString(data, "order_notes"),
            ["client_name"] = GetString(data, "client_name"),
            ["client_code"] = GetString(data, "client_code"),
            ["client_mobile"] = GetString(data, "client_mobile"),
            ["client_national_id"] = GetOptionalString(data, "client_national_id"),
            ["shipping_address"] = GetString(data, "shipping_address"),
            ["district_name"] = GetString(data, "district_name"),
            ["city_name"] = GetString(data, "city_name"),
            ["order_products"] = products,
            ["payment_methods_with_options"] = payments
        };

        return payload;
    }

    public Dictionary<string, object?> BuildUpcPayload(OrderDraft draft)
    {
        var data = draft.OrderData;
        var products = draft.Products.Select(FormatProduct).ToList();
        var payments = draft.Payments.Select(FormatPayment).ToList();

        var payload = new Dictionary<string, object?>
        {
            ["branch_code"] = GetString(data, "branch_code"),
            ["order_code"] = GetString(data, "order_code"),
            ["parent_order_code"] = GetOptionalString(data, "parent_order_code"),
            ["order_delivery_cost"] = GetDecimal(data, "order_delivery_cost"),
            ["is_delivery"] = GetBool(data, "is_delivery", true),
            ["order_status"] = GetString(data, "order_status", "1"),
            ["order_payment_status"] = GetString(data, "order_payment_status", "1"),
            ["order_notes"] = GetOptionalString(data, "order_notes"),
            ["client_name"] = GetString(data, "client_name"),
            ["client_code"] = GetString(data, "client_code"),
            ["client_mobile"] = GetString(data, "client_mobile"),
            ["client_national_id"] = GetOptionalString(data, "client_national_id"),
            ["shipping_address"] = GetString(data, "shipping_address"),
            ["district_name"] = GetString(data, "district_name"),
            ["city_name"] = GetString(data, "city_name"),
            ["order_products"] = products,
            ["payment_methods_with_options"] = payments
        };

        return payload;
    }

    private static string GetString(Dictionary<string, object?> dict, string key, string defaultValue = "")
    {
        return dict.TryGetValue(key, out var val) && val != null ? val.ToString()! : defaultValue;
    }

    private static string? GetOptionalString(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var val) || val == null) return null;
        var str = val.ToString();
        return string.IsNullOrWhiteSpace(str) ? null : str;
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
