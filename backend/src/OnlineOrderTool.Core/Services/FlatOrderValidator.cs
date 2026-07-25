using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Services;

public interface IFlatOrderValidator
{
    List<string> ValidatePayload(Dictionary<string, object?> payload, string moduleKey);
}

public class FlatOrderValidator : IFlatOrderValidator
{
    public List<string> ValidatePayload(Dictionary<string, object?> payload, string moduleKey)
    {
        var errors = new List<string>();

        if (!payload.TryGetValue("branch_code", out var b) || string.IsNullOrWhiteSpace(b?.ToString()))
            errors.Add("Missing required field: branch_code");

        if (!payload.TryGetValue("order_code", out var o) || string.IsNullOrWhiteSpace(o?.ToString()))
            errors.Add("Missing required field: order_code");

        if (!payload.TryGetValue("client_name", out var cn) || string.IsNullOrWhiteSpace(cn?.ToString()))
            errors.Add("Missing required field: client_name");

        if (!payload.TryGetValue("client_code", out var cc) || string.IsNullOrWhiteSpace(cc?.ToString()))
            errors.Add("Missing required field: client_code");

        if (!payload.TryGetValue("client_mobile", out var cm) || string.IsNullOrWhiteSpace(cm?.ToString()))
            errors.Add("Missing required field: client_mobile");

        if (!payload.TryGetValue("shipping_address", out var sa) || string.IsNullOrWhiteSpace(sa?.ToString()))
            errors.Add("Missing required field: shipping_address");

        if (!payload.TryGetValue("order_products", out var prodsObj) || prodsObj is not List<Dictionary<string, object?>> prods || prods.Count == 0)
            errors.Add("Order must contain at least one product.");

        if (!payload.TryGetValue("payment_methods_with_options", out var paysObj) || paysObj is not List<Dictionary<string, object?>> payments || payments.Count == 0)
        {
            errors.Add("Order must contain at least one payment method.");
        }
        else
        {
            foreach (var p in payments)
            {
                var method = p.GetValueOrDefault("payment_method")?.ToString() ?? "";
                var status = p.GetValueOrDefault("payment_status")?.ToString() ?? "";

                if (moduleKey == "upc_ecommerce" && method == "PostToCredit")
                {
                    errors.Add("PostToCredit payment method is not allowed for UPC E-Commerce.");
                }

                if (method == "CashOnDelivery" && status != "not_payment")
                {
                    errors.Add("CashOnDelivery payment status must be 'not_payment'.");
                }

                if ((method is "Visa" or "Tamara" or "Tabby") && status != "done_payment")
                {
                    errors.Add($"{method} payment status must be 'done_payment'.");
                }

                if (method == "PostToCredit" && moduleKey == "ghc_ecommerce")
                {
                    var custName = p.GetValueOrDefault("customer_name")?.ToString();
                    var custNum = p.GetValueOrDefault("customer_number")?.ToString();
                    if (string.IsNullOrWhiteSpace(custName) || string.IsNullOrWhiteSpace(custNum))
                    {
                        errors.Add("PostToCredit requires customer_name and customer_number.");
                    }
                }
            }

            var tamaraCount = payments.Count(p => p.GetValueOrDefault("payment_method")?.ToString() == "Tamara");
            var tabbyCount = payments.Count(p => p.GetValueOrDefault("payment_method")?.ToString() == "Tabby");

            if (tamaraCount > 1) errors.Add("Only one Tamara payment method is allowed.");
            if (tabbyCount > 1) errors.Add("Only one Tabby payment method is allowed.");
        }

        return errors;
    }
}
