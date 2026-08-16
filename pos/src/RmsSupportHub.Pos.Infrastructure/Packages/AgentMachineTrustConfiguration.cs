using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Packages;

/// <summary>
/// Immutable snapshot of the machine-owned package trust configuration. The permanent Agent
/// derives its package lifecycle deployment mode and pinned signer thumbprints exclusively from
/// this validated snapshot.
/// </summary>
public sealed record AgentMachineTrustConfiguration(
    string DeploymentMode,
    string? ProductionSignerThumbprint,
    string? TestingSignerThumbprint,
    string TrustConfigurationPath)
{
    public string? GetConfiguredThumbprint(string channel) => channel switch
    {
        AgentProductIdentity.ReleaseChannelProduction => ProductionSignerThumbprint,
        AgentProductIdentity.ReleaseChannelTesting => TestingSignerThumbprint,
        _ => null
    };

    public string ActiveSignerThumbprint => DeploymentMode switch
    {
        AgentProductIdentity.ReleaseChannelProduction => ProductionSignerThumbprint
            ?? throw new InvalidOperationException("Production deployment mode requires a configured productionSignerThumbprint."),
        AgentProductIdentity.ReleaseChannelTesting => TestingSignerThumbprint
            ?? throw new InvalidOperationException("Testing deployment mode requires a configured testingSignerThumbprint."),
        _ => throw new InvalidOperationException($"The deployment mode '{DeploymentMode}' is unsupported.")
    };
}
