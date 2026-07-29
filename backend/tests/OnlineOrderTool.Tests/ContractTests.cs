using System.Runtime.CompilerServices;
using System.Text.Json;
using Moq;
using OnlineOrderTool.Core.Models;
using OnlineOrderTool.Core.Modules;
using OnlineOrderTool.Core.Repositories;
using OnlineOrderTool.Core.Services;
using Xunit;

namespace OnlineOrderTool.Tests;

/// <summary>
/// These tests pin the API contract. The only sources of truth are the reference
/// payloads under docs/request_examples/** (mirrored here as fixtures/payloads/**)
/// and the verified SQL Server schema in docs/database-schema.md
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

    /// <summary>UpcOrderValidationRepository.cs (which read RequestOrderHeaders-
    /// first and referenced invented columns) was deleted in R5;
    /// OrderRequestRepository.cs (R4) is now the sole reader of OrderRequests /
    /// RequestOrderHeaders / RequestOrderDetails / RequestOrderTransactions /
    /// Invoices, so it is the file this guard must protect.</summary>
    [Fact]
    public void OrderRequestRepository_DoesNotReferenceInventedColumns()
    {
        var repoRoot = GetRepoRoot();
        var sourcePath = Path.Combine(
            repoRoot, "backend", "src", "OnlineOrderTool.Data", "Repositories", "OrderRequestRepository.cs");

        Assert.True(File.Exists(sourcePath), $"Expected to find {sourcePath}");
        var source = File.ReadAllText(sourcePath);

        // These column/table references do not exist in the verified schema
        // (docs/database-schema.md "Verified schema"). Every one of
        // them caused a live "Invalid column name" SqlException in the pre-R4
        // UpcOrderValidationRepository this file replaced.
        var invented = new[]
        {
            "H.Status", "CreatedDateTime", "CustomerMobile", "CustomerName",
            "ShippingAddress", "H.Notes", "UpdatedDateTime", "I.OrderNumber",
            "ItemCode", "DiscountAmount", "VatAmount", "LineTotal", "TransactionId"
        };

        var found = invented.Where(token => source.Contains(token, StringComparison.Ordinal)).ToList();

        Assert.True(found.Count == 0,
            "OrderRequestRepository.cs references invented column names that do not exist " +
            "in the verified schema: " + string.Join(", ", found) +
            ". See docs/database-schema.md \"Verified schema\".");
    }

    /// <summary>
    /// U0 (UI_Rework_Plan.md D5): the UPC Testing API host was wrong (the
    /// third octet was 9 instead of 10), and this repository's own SQL
    /// contract doc had documented the SQL Server host with the fourth octet
    /// wrong too (8 instead of 10), under the mistaken belief that host was
    /// stale. Live re-verification during U0 proved the corrected host only
    /// answers on the RMS HTTP API port (8080) -- the SQL Server itself has
    /// nothing listening on port 1433 there, while the original "8" host
    /// answers a real login and returned live <c>dbo.Branches</c> data. So
    /// this guard only scans source under <c>backend/src/**</c> and
    /// <c>backend/tests/**</c> (never <c>docs/database-schema.md</c> or
    /// <c>README.md</c>, where the original "8" host is the correct,
    /// currently-verified SQL host) for the two stale-host tokens. This test's
    /// own file is excluded from the scan since it necessarily holds those
    /// tokens as literal data for the check below.
    /// </summary>
    [Fact]
    public void BackendSource_DoesNotReferenceStaleRmsHost()
    {
        var repoRoot = GetRepoRoot();
        var searchRoots = new[]
        {
            Path.Combine(repoRoot, "backend", "src"),
            Path.Combine(repoRoot, "backend", "tests"),
        };
        var staleTokens = new[] { "10.10.9.181", "10.10.8.181" };
        var selfPath = Path.Combine(repoRoot, "backend", "tests", "OnlineOrderTool.Tests", "ContractTests.cs");
        var offenders = new List<string>();

        foreach (var root in searchRoots)
        {
            if (!Directory.Exists(root)) continue;

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(file, selfPath, StringComparison.OrdinalIgnoreCase)) continue;

                var relative = Path.GetRelativePath(repoRoot, file);
                var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (segments.Any(seg => seg is "bin" or "obj")) continue;

                string content;
                try
                {
                    content = File.ReadAllText(file);
                }
                catch (IOException)
                {
                    continue;
                }

                if (staleTokens.Any(token => content.Contains(token, StringComparison.Ordinal)))
                {
                    offenders.Add(relative);
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "The following tracked backend files still reference a stale RMS host " +
            "(10.10.9.181, or the mistakenly-flagged 10.10.8.181) and must use " +
            "10.10.10.181 for API endpoints: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// U1 fixes D3/D4 by making the default explicit rather than positional
    /// (<c>Environments.Values.First(e => e.Available)</c>, which is
    /// currently "UPC Production" because dictionary insertion order puts it
    /// first). U0 adds this test failing on purpose -- do not add [Skip] --
    /// so U1 has a real green/red gate for the fix.
    /// </summary>
    [Fact]
    public void UpcEcommerceModule_DefaultEnvironment_IsTesting()
    {
        var module = new UpcEcommerceModule(
            new FlatOrderPayloadBuilder(),
            new FlatOrderValidator(),
            Mock.Of<IItemRepository>(),
            Mock.Of<IConsumerRepository>());

        var environment = module.GetEnvironment(null);

        Assert.Equal("Testing", environment.Environment);
    }
}
