namespace OnlineOrderTool.Core.Modules;

public interface IModuleRegistry
{
    IReadOnlyCollection<IOrderModule> GetAllModules();
    IOrderModule? GetModule(string key);
    IOrderModule GetModuleOrThrow(string key);
}

public class ModuleRegistry : IModuleRegistry
{
    private readonly Dictionary<string, IOrderModule> _modules = new(StringComparer.OrdinalIgnoreCase);

    public ModuleRegistry()
    {
        Register(new GhcEcommerceModule());
        Register(new UpcEcommerceModule());
        Register(new GhcUnicommerceModule());
        Register(new OmsModule());
        Register(new CallCenterModule());
    }

    private void Register(IOrderModule module)
    {
        _modules[module.Key] = module;
    }

    public IReadOnlyCollection<IOrderModule> GetAllModules() => _modules.Values;

    public IOrderModule? GetModule(string key)
    {
        _modules.TryGetValue(key, out var module);
        return module;
    }

    public IOrderModule GetModuleOrThrow(string key)
    {
        return GetModule(key) ?? throw new KeyNotFoundException($"Module with key '{key}' was not found.");
    }
}
