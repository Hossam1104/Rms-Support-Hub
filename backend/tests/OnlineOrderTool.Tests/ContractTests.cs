using System.Runtime.CompilerServices;
using System.Text.Json;
using OnlineOrderTool.Core.Models;
using OnlineOrderTool.Core.Services;
using Xunit;

namespace OnlineOrderTool.Tests;

/// <summary>
/// These tests pin the API contract. The only sources of truth are the reference
/// payloads under docs/request_examples/** (mirrored here as fixtures/payloads/**)
/// and the verified SQL Server schema in docs/Prompts/UPC_Enhancments_Plan.md
/// "Schema discovery". Nothing about the current payload builders or repositories
/// may be treated as correct just because it compiles or another test asserts it.
///
/// As of R0 these are EXPECTED TO FAIL: the current FlatOrderPayloadBuilder emits
/// an invented key set, and UpcOrderValidationRepository queries columns that do
/// not exist. Do not add [Skip] to make them pass — R1/R3 exist to fix the
/// production code until these go green.
/// </summary>
public class ContractTests
{
    private static string FixturesRoot => Path.Combine(AppContext.BaseDirectory, "fixtures", "payloads");

    private static JsonElement LoadFixture(string relativePath)
    {
        var path = Path.Combine(FixturesRoot, relativePath);
        Assert.True(File.Exists(path), $"Fixture not found at '{path}'. Was it copied by CopyToOutputDirectory?");
        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static HashSet<string> ObjectKeys(JsonElement element)
    {
        return element.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> DictKeys(Dictionary<string, object?> dict) =>
        dict.Keys.ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Asserts <paramref name="actual"/> matches <paramref name="expected"/> exactly,
    /// and if not, fails with the missing/unexpected key lists — this diff is the
    /// working spec for whoever fixes the builder.
    /// </summary>
    private static void AssertKeysMatch(string label, HashSet<string> expected, HashSet<string> actual)
    {
        var missing = expected.Except(actual).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var unexpected = actual.Except(expected).OrderBy(k => k, StringComparer.Ordinal).ToList();

        if (missing.Count == 0 && unexpected.Count == 0) return;

        var message = $"[{label}] key set mismatch against the reference payload.\n" +
            $"  missing    ({missing.Count}): {(missing.Count == 0 ? "(none)" : string.Join(", ", missing))}\n" +
            $"  unexpected ({unexpected.Count}): {(unexpected.Count == 0 ? "(none)" : string.Join(", ", unexpected))}";

        Assert.Fail(message);
    }

    private static OrderDraft BuildRepresentativeFlatDraft()
    {
        return new OrderDraft
        {
            OrderData = new Dictionary<string, object?>
            {
                ["branch_code"] = "P900",
                ["order_code"] = "B4",
                ["order_delivery_cost"] = 11.5m,
                ["is_delivery"] = true,
                ["order_status"] = "new",
                ["order_payment_status"] = "done_payment",
                ["order_notes"] = "Don't Ring the bell",
                ["delivery_date"] = "2026-07-25",
                ["fullfilment_plant"] = "PLANT-1"
            },
            Products = new List<Product>
            {
                new()
                {
                    ItemCode = "000000000000212401",
                    ItemName = "Beesline F/Cr.Future Barrier 50Gm",
                    Quantity = 2m,
                    UnitPrice = 175.0m,
                    VatPercentage = 15m
                }
            },
            Payments = new List<Payment>
            {
                new()
                {
                    PaymentMethod = "visa",
                    PaymentStatus = "done_payment",
                    PaymentAmount = 414.0m,
                    PaymentOption = "visa"
                }
            }
        };
    }

    [Fact]
    public void GhcPayload_MatchesReferenceContract_KeyForKey()
    {
        var reference = LoadFixture(Path.Combine("GHC E-Commerce", "request_body.json"));
        var builder = new FlatOrderPayloadBuilder();
        var payload = builder.BuildPayload(BuildRepresentativeFlatDraft(), FlatVariant.GhcVariant);

        AssertKeysMatch("GHC top-level", ObjectKeys(reference), DictKeys(payload));

        var refProduct = reference.GetProperty("order_products")[0];
        var actualProducts = Assert.IsType<List<Dictionary<string, object?>>>(payload["order_products"]);
        AssertKeysMatch("GHC order_products[0]", ObjectKeys(refProduct), DictKeys(actualProducts[0]));

        var refPayment = reference.GetProperty("payment_methods_with_options")[0];
        var actualPayments = Assert.IsType<List<Dictionary<string, object?>>>(payload["payment_methods_with_options"]);
        AssertKeysMatch("GHC payment_methods_with_options[0]", ObjectKeys(refPayment), DictKeys(actualPayments[0]));
    }

    [Fact]
    public void UpcPayload_MatchesReferenceContract_KeyForKey()
    {
        var reference = LoadFixture(Path.Combine("UPC", "4- Invoice without discount, with delivery and paid by visa.json"));
        var builder = new FlatOrderPayloadBuilder();
        var payload = builder.BuildPayload(BuildRepresentativeFlatDraft(), FlatVariant.UpcVariant);

        AssertKeysMatch("UPC top-level", ObjectKeys(reference), DictKeys(payload));

        var refProduct = reference.GetProperty("order_products")[0];
        var actualProducts = Assert.IsType<List<Dictionary<string, object?>>>(payload["order_products"]);
        AssertKeysMatch("UPC order_products[0]", ObjectKeys(refProduct), DictKeys(actualProducts[0]));

        var refPayment = reference.GetProperty("payment_methods_with_options")[0];
        var actualPayments = Assert.IsType<List<Dictionary<string, object?>>>(payload["payment_methods_with_options"]);
        AssertKeysMatch("UPC payment_methods_with_options[0]", ObjectKeys(refPayment), DictKeys(actualPayments[0]));
    }

    /// <summary>
    /// Locates the repository root by walking up from this source file's own path
    /// (via CallerFilePath), independent of build configuration or output layout.
    /// </summary>
    private static string GetRepoRoot([CallerFilePath] string thisFile = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "backend")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new InvalidOperationException(
                $"Could not locate the repository root by walking up from '{thisFile}'.");
        }

        return dir.FullName;
    }

    [Fact]
    public void UpcOrderValidationRepository_DoesNotReferenceInventedColumns()
    {
        var repoRoot = GetRepoRoot();
        var sourcePath = Path.Combine(
            repoRoot, "backend", "src", "OnlineOrderTool.Data", "Repositories", "UpcOrderValidationRepository.cs");

        Assert.True(File.Exists(sourcePath), $"Expected to find {sourcePath}");
        var source = File.ReadAllText(sourcePath);

        // These column/table references do not exist in the verified schema
        // (docs/Prompts/UPC_Enhancments_Plan.md "Schema discovery"). Every one of
        // them causes a live "Invalid column name" SqlException today.
        var invented = new[]
        {
            "H.Status", "CreatedDateTime", "CustomerMobile", "CustomerName",
            "ShippingAddress", "H.Notes", "UpdatedDateTime", "I.OrderNumber",
            "ItemCode", "DiscountAmount", "VatAmount", "LineTotal", "TransactionId"
        };

        var found = invented.Where(token => source.Contains(token, StringComparison.Ordinal)).ToList();

        Assert.True(found.Count == 0,
            "UpcOrderValidationRepository.cs still references invented column names that do not exist " +
            "in the verified schema: " + string.Join(", ", found) +
            ". See docs/database-schema.md and docs/Prompts/UPC_Enhancments_Plan.md \"Schema discovery\".");
    }
}
