namespace OnlineOrderTool.Core.Models;

public class Product
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPercentage { get; set; }
    public decimal Discount { get; set; }
    public string? OfferCode { get; set; }
    public string? OfferMessage { get; set; }

    public decimal UnitVat => Math.Round((UnitPrice - Discount) * (VatPercentage / 100m), 2);
    public decimal TotalVat => Math.Round(UnitVat * Quantity, 2);
    public decimal EstimatedTotal => Math.Round(((UnitPrice - Discount) * Quantity) + TotalVat, 2);
}
