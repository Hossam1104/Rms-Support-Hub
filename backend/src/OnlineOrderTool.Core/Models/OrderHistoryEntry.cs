namespace OnlineOrderTool.Core.Models;

public class OrderHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderCode { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ModuleKey { get; set; } = string.Empty;
    public string EnvironmentKey { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string RequestPayloadJson { get; set; } = string.Empty;
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBodyJson { get; set; }
    public bool IsCancelled { get; set; }
    public string? CancelResponseJson { get; set; }
}
