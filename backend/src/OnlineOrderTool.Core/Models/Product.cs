namespace OnlineOrderTool.Core.Models;

public class Product
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPercentage { get; set; }

    /// <summary>The row's flat total discount (contract field: row_total_discount) —
    /// applied once to the row subtotal, not multiplied per unit.</summary>
    public decimal Discount { get; set; }
    public string? OfferCode { get; set; }
    public string? OfferMessage { get; set; }

    /// <summary>quantity * unit_price, before discount or VAT.</summary>
    public decimal RowSubtotal => Quantity * UnitPrice;

    /// <summary>total_vat_amount: VAT on (subtotal - row discount), matching
    /// the reference calculation — discount is row-level, applied once.</summary>
    public decimal TotalVat => Math.Round((RowSubtotal - Discount) * (VatPercentage / 100m), 2);

    /// <summary>unit_vat_amount: total_vat_amount spread evenly across quantity.</summary>
    public decimal UnitVat => Quantity > 0 ? Math.Round(TotalVat / Quantity, 2) : 0m;

    /// <summary>row_net_total: subtotal - discount + VAT.</summary>
    public decimal EstimatedTotal => Math.Round(RowSubtotal - Discount + TotalVat, 2);
}
