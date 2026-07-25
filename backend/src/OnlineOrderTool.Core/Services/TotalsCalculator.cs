using System.Globalization;
using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Services;

public record TotalsSummary(
    decimal TotalProductAmount,
    decimal TotalProductVat,
    decimal OrderDiscount,
    decimal DeliveryCost,
    decimal TotalOrderAmount,
    decimal TotalPaidAmount,
    decimal RemainingBalance
);

/// <summary>Aligned with calculate_product_totals / calculate_payment_summary
/// in _legacy_flask/modules/flat_order.py. Discount is a flat row-level
/// amount (see Product.Discount / Product.RowSubtotal), applied once per row
/// -- not multiplied by quantity. TotalOrderAmount = sum(row subtotal - row
/// discount) + sum(row VAT) + delivery, which is algebraically the same as
/// legacy's sum(row_net_total) + delivery_cost.</summary>
public static class TotalsCalculator
{
    public static TotalsSummary Calculate(OrderDraft draft)
    {
        var totalProdAmount = Math.Round(draft.Products.Sum(p => p.RowSubtotal - p.Discount), 2);
        var totalProdVat = Math.Round(draft.Products.Sum(p => p.TotalVat), 2);
        var orderDiscount = Math.Round(draft.Products.Sum(p => p.Discount), 2);

        decimal deliveryCost = 0m;
        if (draft.OrderData.TryGetValue("order_delivery_cost", out var dVal) && dVal != null
            && decimal.TryParse(dVal.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            deliveryCost = Math.Round(d, 2);
        }

        var totalOrderAmount = Math.Round(totalProdAmount + totalProdVat + deliveryCost, 2);
        var totalPaidAmount = Math.Round(draft.Payments.Sum(p => p.PaymentAmount), 2);
        // Legacy clamps remaining_amount at 0 (round(max(0, total - paid), 2))
        // rather than letting it go negative on overpayment.
        var remainingBalance = Math.Round(Math.Max(0m, totalOrderAmount - totalPaidAmount), 2);

        return new TotalsSummary(totalProdAmount, totalProdVat, orderDiscount, deliveryCost, totalOrderAmount, totalPaidAmount, remainingBalance);
    }
}
