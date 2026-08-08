namespace RmsSupportHub.Core.Models;

public class DeliveryDetails
{
    public string? DeliveryPhoneNumber { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? DeliveryLocationUrl { get; set; }
    public string? DeliveryNotes { get; set; }
    public decimal DeliveryFees { get; set; }
}

public class UniCommerceInvoice
{
    public string ReferenceNumber { get; set; } = string.Empty;
    public string OnlineOrderNumber { get; set; } = string.Empty;
    public bool IsReturn { get; set; }
    public string? ParentReferenceNumber { get; set; }
    public string? OrderCreationDate { get; set; }
    public string CustomerName { get; set; } = "AMAZON";
    public decimal PaidOnlineAmount { get; set; }
    public decimal PaidWithPointsAmount { get; set; }

    public Consumer Consumer { get; set; } = new();
    public DeliveryDetails Delivery { get; set; } = new();
    public List<RowItem> RowItems { get; set; } = new();

    public decimal GrossAmount => Math.Round(RowItems.Sum(r => r.GrossAmount), 2);
    public decimal TotalDiscount => Math.Round(RowItems.Sum(r => r.RowTotalDiscount), 2);
    public decimal TotalVat => Math.Round(RowItems.Sum(r => r.RowTotalVat), 2);
    public decimal NetAmount => Math.Round(RowItems.Sum(r => r.NetAmount) + Delivery.DeliveryFees, 2);
    public decimal CustomerCreditAmount => Math.Round(NetAmount - PaidOnlineAmount - PaidWithPointsAmount, 2);
}
