namespace OnlineOrderTool.Core;

/// <summary>Pure normalization helpers shared by the item/consumer
/// repositories. Ported line-for-line from _legacy_flask/modules/flat_order.py
/// (normalize_upc_material_number, normalize_phone_search).</summary>
public static class Normalizers
{
    /// <summary>UPC's MaterialNumber is always 18 digits (typically 12 leading
    /// zeros + a 6-digit item number, e.g. "000000000000212401"). Accepts
    /// either the full 18-digit code or the bare 6-digit item number, padding
    /// the latter with 12 leading zeros.</summary>
    public static string NormalizeUpcMaterialNumber(string raw)
    {
        var value = (raw ?? "").Trim();
        if (value.Length == 0 || !value.All(char.IsDigit))
        {
            throw new ArgumentException("Item number must be numeric.", nameof(raw));
        }
        if (value.Length == 18) return value;
        if (value.Length == 6) return value.PadLeft(18, '0');
        throw new ArgumentException(
            "Item number must be either 6 digits or the full 18-digit material number.", nameof(raw));
    }

    /// <summary>Canonicalizes a phone number for search regardless of how it
    /// was typed: strips every non-digit character, then takes the last 9
    /// digits, so "+966556028080" / "966556028080" / "0556028080" /
    /// "556028080" all collapse to the same value -- matched against the
    /// stored column via SQL "RIGHT(column, 9) = @p".</summary>
    public static string NormalizePhoneSearch(string phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length < 9)
        {
            throw new ArgumentException("Phone number is too short.", nameof(phone));
        }
        return digits[^9..];
    }
}
