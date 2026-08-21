namespace RmsSupportHub.Core.Models;

public class RowItem
{
    public decimal Quantity { get; set; }
    public string MaterialNumber { get; set; } = string.Empty;
    public decimal ItemPrice { get; set; }
    public decimal ItemDiscount { get; set; }
    public decimal VatPercentage { get; set; }
    public string? BatchNumber { get; set; }
    public string? ExpireDate { get; set; }
    public string? SerialNumber { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? ScannedCode { get; set; }
    public string? OfferIdentifier { get; set; }

    public decimal GrossAmount => Math.Round(Quantity * ItemPrice, 2);
    public decimal RowTotalDiscount => Math.Round(ItemDiscount * Quantity, 2);
    /// <summary>Uni-Commerce's contract preserves VAT at four decimal
    /// places. Do not round ItemVat to currency precision before multiplying
    /// by quantity.</summary>
    public decimal ItemVat => Math.Round((ItemPrice - ItemDiscount) * (VatPercentage / 100m), 4);
    public decimal RowTotalVat => Math.Round(ItemVat * Quantity, 4);
    public decimal NetAmount => Math.Round(GrossAmount - RowTotalDiscount + RowTotalVat, 4);
}
