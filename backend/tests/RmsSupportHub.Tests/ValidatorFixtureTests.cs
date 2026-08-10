using System.Globalization;
using System.Text.Json;
using RmsSupportHub.Core.Services;
using Xunit;

namespace RmsSupportHub.Tests;

/// <summary>
/// Wires docs/request_examples/edge_cases.json, invalid_payloads.json and the
/// full GHC Uni-Commerce reference set into the validators, per R2 step 5/6.
///
/// edge_cases.json and invalid_payloads.json are payment-rule-focused snippets
/// -- they never include branch_code/order_code/client identity/order_products,
/// since that is not what they are testing. MergeRequiredDefaults patches only
/// the missing supporting fields so the "at least one product" / required-field
/// checks do not drown out the payment-rule assertions these fixtures exist for.
/// </summary>
public class ValidatorFixtureTests
{
    private readonly FlatOrderValidator _flatValidator = new();
    private readonly UniCommerceValidator _uniValidator = new();

    private static string FixturesRoot => Path.Combine(AppContext.BaseDirectory, "fixtures", "payloads");

    private static JsonElement LoadFixtureArray(string fileName)
    {
        var path = Path.Combine(FixturesRoot, fileName);
        Assert.True(File.Exists(path), $"Fixture not found at '{path}'.");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    /// <summary>Converts a JsonElement tree into the same shape
    /// FlatOrderPayloadBuilder/UniCommercePayloadBuilder produce: nested JSON
    /// objects become Dictionary&lt;string, object?&gt;, arrays-of-objects
    /// become List&lt;Dictionary&lt;string, object?&gt;&gt; (not List&lt;object?&gt;,
    /// which the validators' `is List&lt;Dictionary&lt;...&gt;&gt;` pattern
    /// matches would otherwise reject due to generic invariance).</summary>
    private static object? ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>();
                foreach (var prop in element.EnumerateObject())
                    dict[prop.Name] = ConvertJsonElement(prop.Value);
                return dict;
            case JsonValueKind.Array:
                var items = element.EnumerateArray().Select(ConvertJsonElement).ToList();
                return items.Count > 0 && items.All(i => i is Dictionary<string, object?>)
                    ? items.Cast<Dictionary<string, object?>>().ToList()
                    : items;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                return element.TryGetDecimal(out var dec) ? dec : element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            default:
                return null;
        }
    }

    private static Dictionary<string, object?> MergeRequiredDefaults(Dictionary<string, object?> payload)
    {
        payload.TryAdd("branch_code", "TEST-BR");
        payload.TryAdd("order_code", "TEST-ORD-1");
        payload.TryAdd("client_phone", "0500000000");
        payload.TryAdd("client_first_name", "Test");
        payload.TryAdd("order_address", "Test Address");

        if (!payload.TryGetValue("order_products", out var prods) || prods is not List<Dictionary<string, object?>> { Count: > 0 })
        {
            payload["order_products"] = new List<Dictionary<string, object?>>
            {
                new() { ["item_code"] = "1", ["item_name"] = "Test Item", ["quantity"] = 1m, ["unit_price"] = 10m }
            };
        }

        return payload;
    }

    private static decimal SumPaymentAmounts(Dictionary<string, object?> payload)
    {
        if (!payload.TryGetValue("payment_methods_with_options", out var paysObj) || paysObj is not List<Dictionary<string, object?>> payments)
            return 0m;

        return Math.Round(payments.Sum(p =>
            p.TryGetValue("payment_amount", out var amt) && amt != null
                && decimal.TryParse(amt.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
                ? d
                : 0m), 2);
    }

    public static IEnumerable<object[]> EdgeCases()
    {
        var array = LoadFixtureArray("edge_cases.json");
        var index = 0;
        foreach (var entry in array.EnumerateArray())
        {
            var description = entry.GetProperty("description").GetString() ?? $"edge case {index}";
            yield return new object[] { description, entry.GetProperty("payload") };
            index++;
        }
    }

    [Theory]
    [MemberData(nameof(EdgeCases))]
    public void EdgeCase_ProducesZeroValidationErrors(string description, JsonElement payloadElement)
    {
        var payload = MergeRequiredDefaults((Dictionary<string, object?>)ConvertJsonElement(payloadElement)!);
        var totalPaid = SumPaymentAmounts(payload);

        var errors = _flatValidator.ValidatePayload(payload, FlatVariant.UpcVariant, totalPaid);

        Assert.True(errors.Count == 0, $"[{description}] expected zero errors, got: {string.Join(" | ", errors)}");
    }

    /// <summary>description -> a keyword expected to appear in at least one
    /// produced error message, tying each fixture entry to its documented
    /// `violation`.</summary>
    public static IEnumerable<object[]> InvalidPayloads()
    {
        var array = LoadFixtureArray("invalid_payloads.json");
        var expectedKeywords = new Dictionary<string, string[]>
        {
            ["INVALID: Credit Payment with Partial Amount"] = new[] { "Credit payments must cover full order amount" },
            ["INVALID: COD with done_payment status"] = new[] { "COD payments must have 'not_payment' status" },
            ["INVALID: Visa with not_payment status"] = new[] { "Visa", "done_payment" },
            ["INVALID: Cash method used instead of COD"] = new[] { "Payment method 'cash' is not allowed", "COD" }
        };

        foreach (var entry in array.EnumerateArray())
        {
            var description = entry.GetProperty("description").GetString()!;
            var violation = entry.GetProperty("violation").GetString()!;
            Assert.True(expectedKeywords.ContainsKey(description), $"No expected-keyword mapping for fixture entry '{description}' -- add one.");
            yield return new object[] { description, violation, expectedKeywords[description], entry.GetProperty("payload") };
        }
    }

    [Theory]
    [MemberData(nameof(InvalidPayloads))]
    public void InvalidPayload_ProducesErrorMatchingDocumentedViolation(
        string description, string violation, string[] expectedKeywords, JsonElement payloadElement)
    {
        var payload = MergeRequiredDefaults((Dictionary<string, object?>)ConvertJsonElement(payloadElement)!);
        var totalPaid = SumPaymentAmounts(payload);

        var errors = _flatValidator.ValidatePayload(payload, FlatVariant.GhcVariant, totalPaid);

        Assert.True(errors.Count > 0, $"[{description}] expected at least one error for violation '{violation}'.");
        Assert.True(
            errors.Any(e => expectedKeywords.All(k => e.Contains(k, StringComparison.OrdinalIgnoreCase))),
            $"[{description}] expected an error mentioning all of [{string.Join(", ", expectedKeywords)}] " +
            $"(documented violation: \"{violation}\"). Got: {string.Join(" | ", errors)}");
    }

    public static IEnumerable<object[]> GhcUnicommerceReferences()
    {
        var dir = Path.Combine(FixturesRoot, "GHC Uni-Commerce");
        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(f => f))
        {
            yield return new object[] { Path.GetFileName(file) };
        }
    }

    [Theory]
    [MemberData(nameof(GhcUnicommerceReferences))]
    public void GhcUnicommerceReference_ValidatesWithZeroErrors(string fileName)
    {
        var path = Path.Combine(FixturesRoot, "GHC Uni-Commerce", fileName);
        var json = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        var payload = (Dictionary<string, object?>)ConvertJsonElement(json)!;

        var errors = _uniValidator.ValidatePayload(payload);

        Assert.True(errors.Count == 0, $"[{fileName}] expected zero errors, got: {string.Join(" | ", errors)}");
    }
}
