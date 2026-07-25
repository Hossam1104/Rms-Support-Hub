using Microsoft.AspNetCore.Mvc;
using OnlineOrderTool.Core.Modules;

namespace OnlineOrderTool.Api;

/// <summary>Replaces "if (moduleKey == \"upc_ecommerce\") ..." guards
/// throughout the controllers (remediation_plan.md B21) with a check against
/// the module's own declared Capabilities.</summary>
public static class CapabilityGuard
{
    public static ObjectResult? Require(IOrderModule module, Func<ModuleCapabilities, bool> capability, string capabilityName)
    {
        if (capability(module.Capabilities)) return null;

        return new ObjectResult(new
        {
            error = $"'{capabilityName}' is not available for module '{module.Key}'."
        })
        {
            StatusCode = StatusCodes.Status501NotImplemented
        };
    }
}
