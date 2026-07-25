using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Modules;

public class OmsModule : IOrderModule
{
    public string Key => "oms";
    public string Label => "OMS (Order Management)";
    public string Client => "OMS";
    public bool Available => false;
    public IReadOnlyDictionary<string, ModuleEnvironment> Environments { get; } = new Dictionary<string, ModuleEnvironment>();

    public ModuleEnvironment GetEnvironment(string? envKey) => throw new NotSupportedException("OMS is not available yet.");
    public OrderDraft DefaultState() => new();
    public Dictionary<string, object?> BuildPayload(OrderDraft draft) => new();
    public List<string> Validate(Dictionary<string, object?> payload) => new();
}

public class CallCenterModule : IOrderModule
{
    public string Key => "call_center";
    public string Label => "Call Center Ordering";
    public string Client => "Call Center";
    public bool Available => false;
    public IReadOnlyDictionary<string, ModuleEnvironment> Environments { get; } = new Dictionary<string, ModuleEnvironment>();

    public ModuleEnvironment GetEnvironment(string? envKey) => throw new NotSupportedException("Call Center is not available yet.");
    public OrderDraft DefaultState() => new();
    public Dictionary<string, object?> BuildPayload(OrderDraft draft) => new();
    public List<string> Validate(Dictionary<string, object?> payload) => new();
}
