using Microsoft.Extensions.DependencyInjection;
using RmsSupportHub.Pos.Agent.Packages;
using RmsSupportHub.Pos.Agent.Repair;

namespace RmsSupportHub.Pos.Agent;

/// <summary>
/// Identifies the SDK's isolated OpenAPI document host. That host is allowed to map endpoint
/// metadata, but it is never allowed to compose the Agent's machine trust or privileged lifecycle.
/// </summary>
internal static class AgentOpenApiHost
{
    private const string DocumentGeneratorAssemblyName = "GetDocument.Insider";

    public static bool IsMetadataOnlyGeneration() => AppDomain.CurrentDomain.GetAssemblies()
        .Any(assembly => string.Equals(
            assembly.GetName().Name,
            DocumentGeneratorAssemblyName,
            StringComparison.Ordinal));

    public static void AddMetadataOnlyLifecycleGuards(IServiceCollection services)
    {
        services.AddSingleton<AgentPackageService>(_ =>
            throw new InvalidOperationException("Agent package lifecycle is unavailable in the metadata-only OpenAPI host."));
        services.AddSingleton<RepairService>(_ =>
            throw new InvalidOperationException("Repair lifecycle is unavailable in the metadata-only OpenAPI host."));
    }
}
