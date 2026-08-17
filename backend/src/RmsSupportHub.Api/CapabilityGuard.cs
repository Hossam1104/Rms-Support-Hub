using Microsoft.AspNetCore.Mvc;
using RmsSupportHub.Core.Modules;

namespace RmsSupportHub.Api;

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
            error = new
            {
                code = "capability_unavailable",
                message = $"The '{capabilityName}' operation is not available for this module.",
                details = (object?)null
            }
        })
        {
            StatusCode = StatusCodes.Status501NotImplemented
        };
    }

    public static RmsSupportHub.Core.Models.ModuleEnvironment RequireEnvironment(
        IEnvironmentPolicy policy,
        IOrderModule module,
        string? environmentKey)
    {
        var resolution = policy.Resolve(module, environmentKey);
        return resolution.Failure switch
        {
            EnvironmentPolicyFailure.None => resolution.Environment!,
            EnvironmentPolicyFailure.InvalidEnvironment => throw new Exceptions.InvalidEnvironmentException(),
            EnvironmentPolicyFailure.EnvironmentNotAllowed => throw new Exceptions.EnvironmentNotAllowedException(),
            _ => throw new Exceptions.EnvironmentUnconfiguredException()
        };
    }
}
