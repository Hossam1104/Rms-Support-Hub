using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Services;
using Xunit;

namespace RmsSupportHub.Tests;

/// <summary>Proves the R6 fix for remediation_plan.md B19/B20: drafts are
/// keyed by (sessionId, moduleKey) under a dedicated root, not a single
/// process-global file, so two sessions editing the same module never
/// overwrite each other.</summary>
public class DraftManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "oot-draft-tests-" + Guid.NewGuid().ToString("N"));
    private readonly DraftManager _manager;

    public DraftManagerTests()
    {
        _manager = new DraftManager(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task TwoSessions_SameModule_DoNotOverwriteEachOther()
    {
        var sessionA = Guid.NewGuid().ToString("N");
        var sessionB = Guid.NewGuid().ToString("N");

        var draftA = new OrderDraft { OrderData = new Dictionary<string, object?> { ["branch_code"] = "A-BRANCH" } };
        var draftB = new OrderDraft { OrderData = new Dictionary<string, object?> { ["branch_code"] = "B-BRANCH" } };

        await _manager.SaveDraftAsync(sessionA, "upc_ecommerce", draftA);
        await _manager.SaveDraftAsync(sessionB, "upc_ecommerce", draftB);

        var reloadedA = await _manager.LoadDraftAsync(sessionA, "upc_ecommerce");
        var reloadedB = await _manager.LoadDraftAsync(sessionB, "upc_ecommerce");

        Assert.Equal("A-BRANCH", reloadedA!.OrderData["branch_code"]?.ToString());
        Assert.Equal("B-BRANCH", reloadedB!.OrderData["branch_code"]?.ToString());
    }

    [Fact]
    public async Task LoadDraftAsync_UnknownSession_ReturnsNull()
    {
        var result = await _manager.LoadDraftAsync(Guid.NewGuid().ToString("N"), "upc_ecommerce");
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveDraftAsync_WritesUnderRootSessionSubdirectory()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        await _manager.SaveDraftAsync(sessionId, "ghc_ecommerce", new OrderDraft());

        var expectedPath = Path.Combine(_root, sessionId, "ghc_ecommerce.json");
        Assert.True(File.Exists(expectedPath), $"Expected draft file at '{expectedPath}'.");
    }

    /// <summary>U2 (UI_Rework_Plan.md D1): PatchOrderDataAsync holds a
    /// per-(sessionId, moduleKey) lock across the whole load-modify-write, so
    /// firing many concurrent single-field patches -- the shape of the old
    /// per-field PUT order-field race, and of a consumer lookup prefilling
    /// several fields at once -- must never lose a field to a stale
    /// interleaved read, and must never throw a file-sharing IOException.</summary>
    [Fact]
    public async Task PatchOrderDataAsync_TwentyConcurrentPatches_AllFieldsPersist()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        const int fieldCount = 20;

        var tasks = Enumerable.Range(0, fieldCount).Select(i =>
            _manager.PatchOrderDataAsync(
                sessionId,
                "upc_ecommerce",
                new Dictionary<string, object?> { [$"field_{i}"] = $"value_{i}" },
                () => new OrderDraft()));

        await Task.WhenAll(tasks);

        var reloaded = await _manager.LoadDraftAsync(sessionId, "upc_ecommerce");
        Assert.NotNull(reloaded);
        for (var i = 0; i < fieldCount; i++)
        {
            Assert.True(reloaded!.OrderData.TryGetValue($"field_{i}", out var value), $"Expected field_{i} to survive 20 concurrent patches.");
            Assert.Equal($"value_{i}", value?.ToString());
        }
    }
}
