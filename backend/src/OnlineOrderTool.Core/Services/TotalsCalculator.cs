using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Services;

public record TotalsSummary(
    decimal TotalProductAmount,
    decimal TotalProductVat,
    decimal DeliveryCost,
    decimal TotalOrderAmount,
    decimal TotalPaidAmount,
    decimal RemainingBalance
);

public static class TotalsCalculator
{
    public static TotalsSummary Calculate(OrderDraft draft)
    {
        var totalProdAmount = Math.Round(draft.Products.Sum(p => (p.UnitPrice - p.Discount) * p.Quantity), 2);
        var totalProdVat = Math.Round(draft.Products.Sum(p => p.TotalVat), 2);

        decimal deliveryCost = 0m;
        if (draft.OrderData.TryGetValue("order_delivery_cost", out var dVal) && dVal != null && decimal.TryParse(dVal.ToString(), out var d))
        {
            deliveryCost = Math.Round(d, 2);
        }

        var totalOrderAmount = Math.Round(totalProdAmount + totalProdVat + deliveryCost, 2);
        var totalPaidAmount = Math.Round(draft.Payments.Sum(p => p.PaymentAmount), 2);
        var remainingBalance = Math.Round(totalOrderAmount - totalPaidAmount, 2);

        return new TotalsSummary(totalProdAmount, totalProdVat, deliveryCost, totalOrderAmount, totalPaidAmount, remainingBalance);
    }
}
