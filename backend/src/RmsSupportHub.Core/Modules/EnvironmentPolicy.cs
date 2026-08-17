using RmsSupportHub.Core.Models;

namespace RmsSupportHub.Core.Modules;

public enum DeploymentTier
{
    Testing,
    Production
}

public enum EnvironmentPolicyFailure
{
    None,
    InvalidEnvironment,
    EnvironmentNotAllowed,
    EnvironmentUnconfigured
}

public sealed record EnvironmentResolution(
    ModuleEnvironment? Environment,
    EnvironmentPolicyFailure Failure)
{
    public bool IsSuccess => Environment is not null && Failure == EnvironmentPolicyFailure.None;
}

/// <summary>
/// The small, shared policy boundary used by every environment-sensitive API
/// path. It receives already-validated server configuration and has no access
/// to HTTP request data other than the registered environment key.
/// </summary>
public interface IEnvironmentPolicy
{
    DeploymentTier DeploymentTier { get; }

    EnvironmentResolution Resolve(IOrderModule module, string? environmentKey);

    IReadOnlyList<ModuleEnvironment> ProjectEnvironments(IOrderModule module);

    bool IsModuleAvailable(IOrderModule module);

    bool IsAllowed(ModuleEnvironment environment);
}

public sealed class EnvironmentPolicy : IEnvironmentPolicy
{
    public EnvironmentPolicy(DeploymentTier deploymentTier)
    {
        DeploymentTier = deploymentTier;
    }

    public DeploymentTier DeploymentTier { get; }

    public EnvironmentResolution Resolve(IOrderModule module, string? environmentKey)
    {
        if (module.Environments.Count == 0)
            return new(null, EnvironmentPolicyFailure.EnvironmentUnconfigured);

        ModuleEnvironment? environment;
        if (!string.IsNullOrWhiteSpace(environmentKey))
        {
            if (!module.Environments.TryGetValue(environmentKey, out environment))
                return new(null, EnvironmentPolicyFailure.InvalidEnvironment);
        }
        else
        {
            environment = module.Environments.Values.FirstOrDefault(candidate => candidate.IsDefault)
                ?? module.Environments.Values.FirstOrDefault(candidate => candidate.Environment != "Production")
                ?? module.Environments.Values.FirstOrDefault();
        }

        if (environment is null)
            return new(null, EnvironmentPolicyFailure.EnvironmentUnconfigured);

        if (!IsAllowed(environment))
            return new(environment, EnvironmentPolicyFailure.EnvironmentNotAllowed);

        if (!environment.Available)
            return new(environment, EnvironmentPolicyFailure.EnvironmentUnconfigured);

        return new(environment, EnvironmentPolicyFailure.None);
    }

    public IReadOnlyList<ModuleEnvironment> ProjectEnvironments(IOrderModule module) =>
        module.Environments.Values
            .Select(environment => environment with
            {
                Available = environment.Available && IsAllowed(environment),
                HealthProbeEnabled = environment.HealthProbeEnabled && IsAllowed(environment)
            })
            .ToList();

    public bool IsModuleAvailable(IOrderModule module) =>
        module.Available && ProjectEnvironments(module).Any(environment => environment.Available);

    public bool IsAllowed(ModuleEnvironment environment) =>
        DeploymentTier != DeploymentTier.Testing ||
        !string.Equals(environment.Environment, "Production", StringComparison.OrdinalIgnoreCase);
}
