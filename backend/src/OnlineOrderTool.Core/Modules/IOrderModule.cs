using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Modules;

public interface IOrderModule
{
    string Key { get; }
    string Label { get; }
    string Client { get; }
    bool Available { get; }
    IReadOnlyDictionary<string, ModuleEnvironment> Environments { get; }

    ModuleEnvironment GetEnvironment(string? envKey);
    OrderDraft DefaultState();
    Dictionary<string, object?> BuildPayload(OrderDraft draft);
    List<string> Validate(Dictionary<string, object?> payload);
}
