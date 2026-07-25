namespace OnlineOrderTool.Core.Models;

public class Payment
{
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal PaymentAmount { get; set; }
    public string? TransactionId { get; set; }
    public string? PaymentOption { get; set; }
    public decimal OptionCommission { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerNumber { get; set; }
}
