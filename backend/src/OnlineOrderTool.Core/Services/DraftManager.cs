using System.Text.Json;
using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Services;

public interface IDraftManager
{
    Task<OrderDraft?> LoadDraftAsync(string moduleKey);
    Task SaveDraftAsync(string moduleKey, OrderDraft draft);
}

public class DraftManager : IDraftManager
{
    private static string GetFilePath(string moduleKey) => $"last_order_{moduleKey}.json";

    public async Task<OrderDraft?> LoadDraftAsync(string moduleKey)
    {
        var path = GetFilePath(moduleKey);
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<OrderDraft>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveDraftAsync(string moduleKey, OrderDraft draft)
    {
        var path = GetFilePath(moduleKey);
        var json = JsonSerializer.Serialize(draft, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }
}
