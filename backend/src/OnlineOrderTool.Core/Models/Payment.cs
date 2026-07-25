namespace OnlineOrderTool.Core.Models;

public class Payment
{
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal PaymentAmount { get; set; }
    public string? TransactionId { get; set; }
    public string? PaymentOption { get; set; }
    public decimal OptionCommission { get; set; }

    /// <summary>Only meaningful for PaymentMethod == "PostToCredit" (GHC only);
    /// serialized nested as credit_customer_info in the GHC payload.</summary>
    public string? CustomerName { get; set; }
    public string? CustomerNumber { get; set; }

    /// <summary>GHC-only card payment metadata (card_name / bank_code).</summary>
    public string? CardName { get; set; }
    public string? BankCode { get; set; }
}
