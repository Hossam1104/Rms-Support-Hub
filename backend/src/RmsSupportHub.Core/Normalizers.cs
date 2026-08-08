namespace RmsSupportHub.Core;

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

    /// <summary>Strips a LEADING Saudi country code from a subscriber number so
    /// the number field carries only the local part -- the reference payloads
    /// hold the country code in its own <c>client_country_code</c> key
    /// ("966") and <c>client_phone</c> as a bare 9-digit local number
    /// ("556028080"), see docs/request_examples/UPC/**.
    ///
    /// Unlike <see cref="NormalizePhoneSearch"/> this is total: it never
    /// throws, and it returns an unrecognised value unchanged (digits only)
    /// rather than forcing it into a Saudi shape. Only these exact leading
    /// forms are stripped, and only at the documented full length, so a "966"
    /// appearing INSIDE a valid local number (e.g. "509661234") and a genuine
    /// non-Saudi number are both left intact:
    ///   "+966XXXXXXXXX" / "966XXXXXXXXX" (12 digits) -> drop "966"
    ///   "00966XXXXXXXXX"                 (14 digits) -> drop "00966"
    ///   "0XXXXXXXXX"                     (10 digits) -> drop "0"
    /// Separators (spaces, dashes, parentheses, "+") are dropped because every
    /// reference payload stores digits only.</summary>
    public static string NormalizeLocalPhone(string? phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());

        if (digits.Length == 14 && digits.StartsWith("00966", StringComparison.Ordinal)) return digits[5..];
        if (digits.Length == 12 && digits.StartsWith("966", StringComparison.Ordinal)) return digits[3..];
        if (digits.Length == 10 && digits.StartsWith("0", StringComparison.Ordinal)) return digits[1..];

        return digits;
    }
}
