using System.Text.Json;
using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Services;

public interface IOrderHistoryService
{
    Task<List<OrderHistoryEntry>> GetHistoryAsync(string moduleKey);
    Task<OrderHistoryEntry?> GetEntryAsync(string moduleKey, Guid id);
    Task<OrderHistoryEntry> AddEntryAsync(string moduleKey, OrderHistoryEntry entry);
    Task MarkCancelledAsync(string moduleKey, Guid id, string cancelResponseJson);
}

public class OrderHistoryService : IOrderHistoryService
{
    private static string GetFilePath(string moduleKey) => $"order_history_{moduleKey}.json";

    public async Task<List<OrderHistoryEntry>> GetHistoryAsync(string moduleKey)
    {
        var path = GetFilePath(moduleKey);
        if (!File.Exists(path)) return new List<OrderHistoryEntry>();

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var items = JsonSerializer.Deserialize<List<OrderHistoryEntry>>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return items?.OrderByDescending(x => x.Timestamp).ToList() ?? new List<OrderHistoryEntry>();
        }
        catch
        {
            return new List<OrderHistoryEntry>();
        }
    }

    public async Task<OrderHistoryEntry?> GetEntryAsync(string moduleKey, Guid id)
    {
        var history = await GetHistoryAsync(moduleKey);
        return history.FirstOrDefault(x => x.Id == id);
    }

    public async Task<OrderHistoryEntry> AddEntryAsync(string moduleKey, OrderHistoryEntry entry)
    {
        var history = await GetHistoryAsync(moduleKey);
        history.Insert(0, entry);

        var path = GetFilePath(moduleKey);
        var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
        await File.WriteAllTextAsync(path, json);

        return entry;
    }

    public async Task MarkCancelledAsync(string moduleKey, Guid id, string cancelResponseJson)
    {
        var history = await GetHistoryAsync(moduleKey);
        var entry = history.FirstOrDefault(x => x.Id == id);
        if (entry != null)
        {
            entry.IsCancelled = true;
            entry.CancelResponseJson = cancelResponseJson;

            var path = GetFilePath(moduleKey);
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
        }
    }
}
