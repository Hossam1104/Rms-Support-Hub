using System.Collections.Concurrent;
using System.Text.Json;
using RmsSupportHub.Core.Models;

namespace RmsSupportHub.Core.Services;

public interface IDraftManager
{
    Task<OrderDraft?> LoadDraftAsync(string sessionId, string moduleKey);
    Task SaveDraftAsync(string sessionId, string moduleKey, OrderDraft draft);

    /// <summary>Applies every entry in <paramref name="fields"/> to
    /// <c>OrderData</c> inside a single load-modify-write, serialised per
    /// (sessionId, moduleKey) so concurrent patches (e.g. a consumer lookup
    /// prefilling nine fields at once) can never race or lose one another.
    /// Returns the draft as persisted.</summary>
    Task<OrderDraft> PatchOrderDataAsync(string sessionId, string moduleKey, IReadOnlyDictionary<string, object?> fields, Func<OrderDraft> defaultState);
}

/// <summary>Drafts are keyed by (sessionId, moduleKey) and stored under a
/// dedicated var/drafts/ root (see remediation_plan.md B19/B20) rather than
/// the process's working directory. sessionId comes from
/// SessionIdMiddleware, which guarantees it is a bare Guid ("N" format)
/// before any controller can reach this class, so it is always safe to use
/// directly as a directory name.
///
/// U2 (UI_Rework_Plan.md D1): every write is serialised per (sessionId,
/// moduleKey) via a keyed SemaphoreSlim and persisted atomically (write to
/// "<file>.tmp", then File.Move(overwrite: true)) so a reader can never
/// observe a partial file and two concurrent writers can never interleave.
/// PatchOrderDataAsync holds that same lock across the whole
/// load-modify-write, not just the write, which is what actually closes the
/// lost-update race -- SaveDraftAsync alone only made the write itself safe.</summary>
public class DraftManager : IDraftManager
{
    private const int MaxWriteAttempts = 5;

    private readonly string _rootPath;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly JsonSerializerOptions WriteJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public DraftManager(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    private string GetFilePath(string sessionId, string moduleKey) =>
        Path.Combine(_rootPath, sessionId, $"{moduleKey}.json");

    private SemaphoreSlim GetLock(string sessionId, string moduleKey) =>
        _locks.GetOrAdd($"{sessionId}:{moduleKey}", static _ => new SemaphoreSlim(1, 1));

    public async Task<OrderDraft?> LoadDraftAsync(string sessionId, string moduleKey)
    {
        var path = GetFilePath(sessionId, moduleKey);
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<OrderDraft>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveDraftAsync(string sessionId, string moduleKey, OrderDraft draft)
    {
        var draftLock = GetLock(sessionId, moduleKey);
        await draftLock.WaitAsync();
        try
        {
            await WriteAtomicAsync(sessionId, moduleKey, draft);
        }
        finally
        {
            draftLock.Release();
        }
    }

    public async Task<OrderDraft> PatchOrderDataAsync(string sessionId, string moduleKey, IReadOnlyDictionary<string, object?> fields, Func<OrderDraft> defaultState)
    {
        var draftLock = GetLock(sessionId, moduleKey);
        await draftLock.WaitAsync();
        try
        {
            var draft = await LoadDraftAsync(sessionId, moduleKey) ?? defaultState();
            foreach (var (fieldName, value) in fields)
            {
                draft.OrderData[fieldName] = value;
            }

            await WriteAtomicAsync(sessionId, moduleKey, draft);
            return draft;
        }
        finally
        {
            draftLock.Release();
        }
    }

    /// <summary>Caller must hold the per-(sessionId, moduleKey) lock. Writes
    /// to a temp file and renames over the target so a concurrent reader
    /// never observes a half-written file, retrying a bounded number of
    /// times on IOException (e.g. a transient Windows file-handle
    /// collision) before letting the error surface.</summary>
    private async Task WriteAtomicAsync(string sessionId, string moduleKey, OrderDraft draft)
    {
        var path = GetFilePath(sessionId, moduleKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmpPath = $"{path}.tmp";
        var json = JsonSerializer.Serialize(draft, WriteJsonOptions);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await File.WriteAllTextAsync(tmpPath, json);
                File.Move(tmpPath, path, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < MaxWriteAttempts)
            {
                await Task.Delay(25 * attempt);
            }
        }
    }
}
