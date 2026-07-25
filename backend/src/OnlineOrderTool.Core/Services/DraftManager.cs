using System.Text.Json;
using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Services;

public interface IDraftManager
{
    Task<OrderDraft?> LoadDraftAsync(string sessionId, string moduleKey);
    Task SaveDraftAsync(string sessionId, string moduleKey, OrderDraft draft);
}

/// <summary>Drafts are keyed by (sessionId, moduleKey) and stored under a
/// dedicated var/drafts/ root (see remediation_plan.md B19/B20) rather than
/// the process's working directory. sessionId comes from
/// SessionIdMiddleware, which guarantees it is a bare Guid ("N" format)
/// before any controller can reach this class, so it is always safe to use
/// directly as a directory name.</summary>
public class DraftManager : IDraftManager
{
    private readonly string _rootPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly JsonSerializerOptions WriteJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public DraftManager(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    private string GetFilePath(string sessionId, string moduleKey) =>
        Path.Combine(_rootPath, sessionId, $"{moduleKey}.json");

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
        var path = GetFilePath(sessionId, moduleKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(draft, WriteJsonOptions);
        await File.WriteAllTextAsync(path, json);
    }
}
