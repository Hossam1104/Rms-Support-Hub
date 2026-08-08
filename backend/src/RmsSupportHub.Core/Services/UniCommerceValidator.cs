using System.Globalization;

namespace RmsSupportHub.Core.Services;

public interface IUniCommerceValidator
{
    List<string> ValidatePayload(Dictionary<string, object?> payload);
}

/// <summary>Ported line-for-line from validate() in
/// _legacy_flask/modules/ghc_unicommerce.py. Field names verified against
/// all 20 examples in docs/request_examples/GHC Uni-Commerce/ -- unlike the
/// flat-order schema, this one was never invented; no rule changes were
/// needed here for R2, only the same decimal-parsing robustness fix applied
/// to FlatOrderValidator/TotalsCalculator.</summary>
public class UniCommerceValidator : IUniCommerceValidator
{
    public List<string> ValidatePayload(Dictionary<string, object?> payload)
    {
        var errors = new List<string>();

        if (!payload.TryGetValue("ReferenceNumber", out var refNum) || string.IsNullOrWhiteSpace(refNum?.ToString()))
            errors.Add("Missing required field: ReferenceNumber");

        if (!payload.TryGetValue("CustomerName", out var custName) || string.IsNullOrWhiteSpace(custName?.ToString()))
            errors.Add("Missing required field: CustomerName");

        if (!payload.TryGetValue("RowItems", out var rowsObj) || rowsObj is not List<Dictionary<string, object?>> rows || rows.Count == 0)
            errors.Add("No row items in the invoice.");

        var isReturn = payload.TryGetValue("IsReturn", out var rVal) && rVal is true;
        if (isReturn)
        {
            if (!payload.TryGetValue("ParentReferenceNumber", out var pRef) || string.IsNullOrWhiteSpace(pRef?.ToString()))
                errors.Add("Returns must reference a ParentReferenceNumber.");
        }

        var netAmount = GetDecimal(payload, "NetAmount");
        var paidOnline = GetDecimal(payload, "PaidOnlineAmount");
        var paidPoints = GetDecimal(payload, "PaidWithPointsAmount");
        var customerCredit = GetDecimal(payload, "CustomerCreditAmount");

        var totalSettled = Math.Round(paidOnline + paidPoints + customerCredit, 2);
        if (Math.Abs(totalSettled - netAmount) > 0.01m)
        {
            errors.Add($"PaidOnlineAmount ({paidOnline}) + PaidWithPointsAmount ({paidPoints}) + CustomerCreditAmount ({customerCredit}) must equal NetAmount ({netAmount}). Current total: {totalSettled}");
        }

        return errors;
    }

    private static decimal GetDecimal(Dictionary<string, object?> dict, string key)
    {
        if (dict.TryGetValue(key, out var val) && val != null
            && decimal.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }
        return 0m;
    }
}
