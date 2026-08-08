namespace RmsSupportHub.Core.Models;

public class OrderDraft
{
    public Dictionary<string, object?> OrderData { get; set; } = new();
    public List<Product> Products { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public Consumer Consumer { get; set; } = new();
    public DeliveryDetails Delivery { get; set; } = new();
    public List<RowItem> RowItems { get; set; } = new();
}
