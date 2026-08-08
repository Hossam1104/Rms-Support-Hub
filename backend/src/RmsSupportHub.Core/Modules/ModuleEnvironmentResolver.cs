using RmsSupportHub.Core.Models;

namespace RmsSupportHub.Core.Modules;

/// <summary>Shared GetEnvironment resolution for every *Module.cs (U1,
/// UI_Rework_Plan.md D3/D4). The old behaviour --
/// <c>Environments.Values.First(e => e.Available)</c> -- silently resolved to
/// Production for UPC/GHC E-Commerce because it happened to be the first
/// entry in the dictionary. The default must now be an explicit
/// <see cref="ModuleEnvironment.IsDefault"/> flag; if a module somehow has
/// none flagged, prefer a non-Production environment before falling back to
/// the first available one.</summary>
public static class ModuleEnvironmentResolver
{
    public static ModuleEnvironment Resolve(IReadOnlyDictionary<string, ModuleEnvironment> environments, string? envKey)
    {
        if (!string.IsNullOrEmpty(envKey) && environments.TryGetValue(envKey, out var requested))
            return requested;

        return environments.Values.FirstOrDefault(e => e.IsDefault)
            ?? environments.Values.FirstOrDefault(e => e.Available && e.Environment != "Production")
            ?? environments.Values.First(e => e.Available);
    }
}
