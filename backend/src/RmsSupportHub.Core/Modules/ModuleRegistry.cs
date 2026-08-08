using RmsSupportHub.Core.Repositories;
using RmsSupportHub.Core.Services;

namespace RmsSupportHub.Core.Modules;

public interface IModuleRegistry
{
    IReadOnlyCollection<IOrderModule> GetAllModules();
    IOrderModule? GetModule(string key);
    IOrderModule GetModuleOrThrow(string key);
}

public class ModuleRegistry : IModuleRegistry
{
    private readonly Dictionary<string, IOrderModule> _modules = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Constructs each module with its own repository/builder/
    /// validator dependencies (all already DI-registered in Program.cs), so
    /// every module is fully self-sufficient -- BuildPayload/Validate/lookups
    /// no longer need a controller to switch on module-key strings to know
    /// which builder or repository to call (see remediation_plan.md B21).</summary>
    public ModuleRegistry(
        IFlatOrderPayloadBuilder flatPayloadBuilder,
        IFlatOrderValidator flatValidator,
        IUniCommercePayloadBuilder uniPayloadBuilder,
        IUniCommerceValidator uniValidator,
        IGhcItemRepository ghcItemRepository,
        IGhcConsumerRepository ghcConsumerRepository,
        IUpcItemRepository upcItemRepository,
        IUpcConsumerRepository upcConsumerRepository)
    {
        Register(new GhcEcommerceModule(flatPayloadBuilder, flatValidator, ghcItemRepository, ghcConsumerRepository));
        Register(new UpcEcommerceModule(flatPayloadBuilder, flatValidator, upcItemRepository, upcConsumerRepository));
        Register(new GhcUnicommerceModule(uniPayloadBuilder, uniValidator));
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
