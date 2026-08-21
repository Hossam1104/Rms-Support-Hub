using RmsSupportHub.Api.Exceptions;
using RmsSupportHub.Core.Modules;

namespace RmsSupportHub.Api.Security;

/// <summary>Enforces the inbound transport boundary for Production mutation
/// operations. Request.IsHttps is evaluated only after the trusted forwarded
/// headers middleware has had a chance to normalize the effective scheme.</summary>
public static class ProductionTransportGuard
{
    public static void RequireHttps(IEnvironmentPolicy environmentPolicy, HttpContext context)
    {
        if (environmentPolicy.DeploymentTier == DeploymentTier.Production
            && !context.Request.IsHttps)
        {
            throw new ProductionSecureTransportRequiredException();
        }
    }
}
