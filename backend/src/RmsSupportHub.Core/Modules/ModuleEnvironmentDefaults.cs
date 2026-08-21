using RmsSupportHub.Core.Models;

namespace RmsSupportHub.Core.Modules;

/// <summary>
/// Immutable module metadata used by the API composition root when it builds
/// the server-owned environment catalog. It deliberately contains no
/// endpoint, connection-string value, or credential. Runtime routing is
/// supplied by the validated SupportHub configuration.
/// </summary>
public static class ModuleEnvironmentDefaults
{
    public static IReadOnlyDictionary<string, ModuleEnvironment> GhcEcommerce() =>
        new Dictionary<string, ModuleEnvironment>(StringComparer.OrdinalIgnoreCase)
        {
            ["GHC Production"] = Create(
                key: "GHC Production",
                environment: "Production",
                description: "GHC live routing.",
                accent: "ember",
                cue: "Warehouse",
                icon: "bi-box-seam",
                routeLabel: "Live lane",
                visualUrl: "static/assets/whites_logo.svg",
                visualAlt: "GHC logo",
                isDefault: false,
                requiresDatabase: true,
                requiresApiEndpoint: true,
                requiresCancelEndpoint: true),
            ["GHC Testing"] = Create(
                key: "GHC Testing",
                environment: "Testing",
                description: "GHC QA routing.",
                accent: "ocean",
                cue: "Dispatch",
                icon: "bi-truck",
                routeLabel: "QA lane",
                visualUrl: "static/assets/whites_logo.svg",
                visualAlt: "GHC logo",
                isDefault: true,
                requiresDatabase: true,
                requiresApiEndpoint: true,
                requiresCancelEndpoint: true)
        };

    public static IReadOnlyDictionary<string, ModuleEnvironment> UpcEcommerce() =>
        new Dictionary<string, ModuleEnvironment>(StringComparer.OrdinalIgnoreCase)
        {
            ["UPC Production"] = Create(
                key: "UPC Production",
                environment: "Production",
                description: "UPC live routing.",
                accent: "sunrise",
                cue: "Retail Ops",
                icon: "bi-bag-check",
                routeLabel: "Live lane",
                visualUrl: "static/assets/upc_logo.svg",
                visualAlt: "UPC logo",
                isDefault: false,
                requiresDatabase: true,
                requiresApiEndpoint: true,
                requiresCancelEndpoint: true),
            ["UPC Testing"] = Create(
                key: "UPC Testing",
                environment: "Testing",
                description: "UPC QA routing.",
                accent: "electric",
                cue: "QA Grid",
                icon: "bi-sliders2-vertical",
                routeLabel: "Test lane",
                visualUrl: "static/assets/upc_logo.svg",
                visualAlt: "UPC logo",
                isDefault: true,
                requiresDatabase: true,
                requiresApiEndpoint: true,
                requiresCancelEndpoint: true)
        };

    public static IReadOnlyDictionary<string, ModuleEnvironment> GhcUnicommerce() =>
        new Dictionary<string, ModuleEnvironment>(StringComparer.OrdinalIgnoreCase)
        {
            ["GHC Uni-Commerce Production"] = Create(
                key: "GHC Uni-Commerce Production",
                environment: "Production",
                description: "GHC Uni-Commerce live routing.",
                accent: "aurora",
                cue: "Automation",
                icon: "bi-cpu",
                routeLabel: "Pending lane",
                visualUrl: "static/assets/whites_logo.svg",
                visualAlt: "GHC Uni-Commerce logo",
                isDefault: false,
                requiresDatabase: true,
                requiresApiEndpoint: true,
                requiresCancelEndpoint: false),
            ["GHC Uni-Commerce Testing"] = Create(
                key: "GHC Uni-Commerce Testing",
                environment: "Testing",
                description: "GHC Uni-Commerce QA routing.",
                accent: "violet",
                cue: "Staging",
                icon: "bi-hourglass-split",
                routeLabel: "QA lane",
                visualUrl: "static/assets/whites_logo.svg",
                visualAlt: "GHC Uni-Commerce logo",
                isDefault: true,
                requiresDatabase: true,
                requiresApiEndpoint: true,
                requiresCancelEndpoint: false)
        };

    private static ModuleEnvironment Create(
        string key,
        string environment,
        string description,
        string accent,
        string cue,
        string icon,
        string routeLabel,
        string visualUrl,
        string visualAlt,
        bool isDefault,
        bool requiresDatabase,
        bool requiresApiEndpoint,
        bool requiresCancelEndpoint) => new()
    {
        Key = key,
        Environment = environment,
        Description = description,
        Accent = accent,
        Cue = cue,
        Icon = icon,
        RouteLabel = routeLabel,
        VisualUrl = visualUrl,
        VisualAlt = visualAlt,
        Available = false,
        IsDefault = isDefault,
        RequiresDatabase = requiresDatabase,
        RequiresApiEndpoint = requiresApiEndpoint,
        RequiresCancelEndpoint = requiresCancelEndpoint,
        HealthProbeEnabled = false
    };
}
