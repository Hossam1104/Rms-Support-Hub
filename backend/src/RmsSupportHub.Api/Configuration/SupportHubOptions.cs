using Microsoft.Extensions.Options;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Modules;

namespace RmsSupportHub.Api.Configuration;

public sealed class SupportHubOptions
{
    public const string SectionName = "SupportHub";

    public string DeploymentTier { get; set; } = "Testing";
    public bool AllowCustomEndpoints { get; set; }
    public HealthProbeOptions HealthProbe { get; set; } = new();
    public Dictionary<string, Dictionary<string, SupportHubEnvironmentOptions>> Environments { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class HealthProbeOptions
{
    public bool Enabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 3;
}

public sealed class SupportHubEnvironmentOptions
{
    public bool Enabled { get; set; }
    public string? ApiEndpointKey { get; set; }
    public string? CancelEndpointKey { get; set; }
    public string? ConnectionStringName { get; set; }
    public string? DatabaseOverride { get; set; }
    public bool AllowCustomEndpoint { get; set; }
}

/// <summary>Validates only the structural server-owned contract. Secret
/// values are deliberately not required at startup so unrelated surfaces can
/// still start; a missing secret becomes a safe environment_unconfigured error
/// when that database environment is selected.</summary>
public sealed class SupportHubOptionsValidator : IValidateOptions<SupportHubOptions>
{
    private readonly IConfiguration _configuration;

    public SupportHubOptionsValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ValidateOptionsResult Validate(string? name, SupportHubOptions options)
    {
        var errors = new List<string>();

        if (!DeploymentTierParser.TryParseExact(options.DeploymentTier, out _))
            errors.Add("SupportHub:DeploymentTier must be Testing or Production.");

        if (options.HealthProbe is null || options.HealthProbe.TimeoutSeconds is < 1 or > 30)
            errors.Add("SupportHub:HealthProbe:TimeoutSeconds must be between 1 and 30.");

        if (options.AllowCustomEndpoints)
            errors.Add("SupportHub:AllowCustomEndpoints must remain false; custom browser endpoints are disabled.");

        var templates = Templates();
        foreach (var moduleEntry in options.Environments ?? new(StringComparer.OrdinalIgnoreCase))
        {
            if (!templates.TryGetValue(moduleEntry.Key, out var moduleTemplates))
            {
                errors.Add($"SupportHub:Environments contains unknown module '{moduleEntry.Key}'.");
                continue;
            }

            foreach (var environmentEntry in moduleEntry.Value ?? new(StringComparer.OrdinalIgnoreCase))
            {
                if (!moduleTemplates.TryGetValue(environmentEntry.Key, out var template))
                {
                    errors.Add($"SupportHub:Environments:{moduleEntry.Key} contains unknown environment '{environmentEntry.Key}'.");
                    continue;
                }

                var registration = environmentEntry.Value;
                if (registration is null) continue;

                if (registration.AllowCustomEndpoint)
                    errors.Add($"Custom endpoints are disabled for '{moduleEntry.Key}/{environmentEntry.Key}'.");

                if (!registration.Enabled) continue;

                var apiUrl = ResolveEndpoint("ModuleEndpoints", registration.ApiEndpointKey);
                var cancelUrl = ResolveEndpoint("ModuleCancelEndpoints", registration.CancelEndpointKey);

                if (template.RequiresApiEndpoint)
                {
                    if (string.IsNullOrWhiteSpace(registration.ApiEndpointKey))
                        errors.Add($"Enabled environment '{moduleEntry.Key}/{environmentEntry.Key}' requires ApiEndpointKey.");
                    else if (!IsSafeHttpUrl(apiUrl))
                        errors.Add($"ModuleEndpoints:{registration.ApiEndpointKey} must be an absolute HTTP(S) URL.");
                }

                if (template.RequiresCancelEndpoint)
                {
                    if (string.IsNullOrWhiteSpace(registration.CancelEndpointKey))
                        errors.Add($"Enabled environment '{moduleEntry.Key}/{environmentEntry.Key}' requires CancelEndpointKey.");
                    else if (!IsSafeHttpUrl(cancelUrl))
                        errors.Add($"ModuleCancelEndpoints:{registration.CancelEndpointKey} must be an absolute HTTP(S) URL.");
                }

                if (template.RequiresDatabase && string.IsNullOrWhiteSpace(registration.ConnectionStringName))
                    errors.Add($"Enabled database environment '{moduleEntry.Key}/{environmentEntry.Key}' requires ConnectionStringName.");

                if (!string.IsNullOrWhiteSpace(registration.DatabaseOverride) &&
                    !System.Text.RegularExpressions.Regex.IsMatch(registration.DatabaseOverride, "^[A-Za-z0-9_]+$"))
                    errors.Add($"DatabaseOverride for '{moduleEntry.Key}/{environmentEntry.Key}' must be a simple database identifier.");
            }
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ModuleEnvironment>> Templates() =>
        new Dictionary<string, IReadOnlyDictionary<string, ModuleEnvironment>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ghc_ecommerce"] = ModuleEnvironmentDefaults.GhcEcommerce(),
            ["upc_ecommerce"] = ModuleEnvironmentDefaults.UpcEcommerce(),
            ["ghc_unicommerce"] = ModuleEnvironmentDefaults.GhcUnicommerce()
        };

    private string? ResolveEndpoint(string section, string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : _configuration[$"{section}:{key}"];

    private static bool IsSafeHttpUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;
        return string.IsNullOrWhiteSpace(uri.UserInfo) &&
               string.IsNullOrWhiteSpace(uri.Fragment) &&
               string.IsNullOrWhiteSpace(uri.Query);
    }
}
