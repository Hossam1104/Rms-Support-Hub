using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Modules;

namespace RmsSupportHub.Api.Configuration;

/// <summary>Projects immutable module metadata onto the validated, server-owned
/// endpoint and database-key registration. No secret value is copied into the
/// module object or returned by any DTO.</summary>
public static class ConfiguredEnvironmentCatalog
{
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ModuleEnvironment>> Build(
        IConfiguration configuration,
        SupportHubOptions options)
    {
        var templates = SupportHubOptionsValidator.Templates();
        var result = new Dictionary<string, IReadOnlyDictionary<string, ModuleEnvironment>>(StringComparer.OrdinalIgnoreCase);

        foreach (var moduleEntry in templates)
        {
            options.Environments.TryGetValue(moduleEntry.Key, out var configuredModule);
            var environments = new Dictionary<string, ModuleEnvironment>(StringComparer.OrdinalIgnoreCase);

            foreach (var templateEntry in moduleEntry.Value)
            {
                SupportHubEnvironmentOptions? registration = null;
                if (configuredModule is not null)
                    configuredModule.TryGetValue(templateEntry.Key, out registration);

                var apiUrl = ResolveEndpoint(configuration, "ModuleEndpoints", registration?.ApiEndpointKey);
                var cancelUrl = ResolveEndpoint(configuration, "ModuleCancelEndpoints", registration?.CancelEndpointKey);
                var enabled = registration?.Enabled == true;
                var secretAvailable = !templateEntry.Value.RequiresDatabase ||
                    (!string.IsNullOrWhiteSpace(registration?.ConnectionStringName) &&
                     !string.IsNullOrWhiteSpace(configuration.GetConnectionString(registration.ConnectionStringName)));
                var structurallyAvailable = enabled &&
                    (!templateEntry.Value.RequiresApiEndpoint || !string.IsNullOrWhiteSpace(apiUrl)) &&
                    (!templateEntry.Value.RequiresCancelEndpoint || !string.IsNullOrWhiteSpace(cancelUrl)) &&
                    secretAvailable;

                environments[templateEntry.Key] = templateEntry.Value with
                {
                    Available = structurallyAvailable,
                    ApiUrl = apiUrl,
                    CancelUrl = cancelUrl,
                    ConnectionStringName = registration?.ConnectionStringName,
                    DatabaseOverride = registration?.DatabaseOverride,
                    HealthProbeEnabled = structurallyAvailable && options.HealthProbe.Enabled && !string.IsNullOrWhiteSpace(apiUrl),
                    HealthProbeTimeout = TimeSpan.FromSeconds(options.HealthProbe.TimeoutSeconds)
                };
            }

            result[moduleEntry.Key] = environments;
        }

        return result;
    }

    private static string? ResolveEndpoint(IConfiguration configuration, string section, string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : configuration[$"{section}:{key}"];
}
