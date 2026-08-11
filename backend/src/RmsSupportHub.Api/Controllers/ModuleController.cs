using Microsoft.AspNetCore.Mvc;
using RmsSupportHub.Api.Middleware;
using RmsSupportHub.Core.DTOs;
using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Services;

namespace RmsSupportHub.Api.Controllers;

[ApiController]
[Route("api/modules")]
public class ModuleController : ControllerBase
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IDraftManager _draftManager;
    private readonly IModuleHealthService _healthService;

    public ModuleController(IModuleRegistry moduleRegistry, IDraftManager draftManager, IModuleHealthService healthService)
    {
        _moduleRegistry = moduleRegistry;
        _draftManager = draftManager;
        _healthService = healthService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<ModuleDto>> GetModules()
    {
        var modules = _moduleRegistry.GetAllModules().Select(m => new ModuleDto(
            Key: m.Key,
            Label: m.Label,
            Client: m.Client,
            Available: m.Available,
            Environments: m.Environments.Values.Select(e => new EnvironmentDto(
                Key: e.Key,
                Environment: e.Environment,
                Description: e.Description,
                Accent: e.Accent,
                Cue: e.Cue,
                Icon: e.Icon,
                RouteLabel: e.RouteLabel,
                VisualUrl: e.VisualUrl,
                VisualAlt: e.VisualAlt,
                Available: e.Available,
                StatusLabel: e.StatusLabel,
                HasApiUrl: !string.IsNullOrWhiteSpace(e.ApiUrl),
                HasCancelUrl: !string.IsNullOrWhiteSpace(e.CancelUrl),
                IsDefault: e.IsDefault
            )).ToList(),
            Capabilities: ToDto(m.Capabilities)
        ));

        return Ok(modules);
    }

    private static ModuleCapabilitiesDto ToDto(ModuleCapabilities c) => new(
        DraftKind: c.DraftKind,
        ItemLookup: c.ItemLookup,
        ConsumerLookup: c.ConsumerLookup,
        OrderRequests: c.OrderRequests,
        Cancel: c.Cancel,
        Resend: c.Resend,
        HasDeliveryFields: c.HasDeliveryFields,
        BranchLookup: c.BranchLookup
    );

    /// <summary>Reachability of every environment's send endpoint, probed from
    /// the API host. Kept off GET /api/modules so the catalog stays instant and
    /// cacheable while the dashboard fills the dots in afterwards. The literal
    /// "health" segment outranks the "{key}" route below, so this never
    /// resolves as a module lookup.</summary>
    [HttpGet("health")]
    public async Task<ActionResult<IEnumerable<EnvironmentHealthDto>>> GetHealth(CancellationToken cancellationToken)
    {
        var health = await _healthService.GetHealthAsync(cancellationToken);
        return Ok(health.Select(h => new EnvironmentHealthDto(h.ModuleKey, h.EnvironmentKey, h.Status, h.CheckedAt)));
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<object>> GetModule(string key)
    {
        var module = _moduleRegistry.GetModule(key);
        if (module == null) return NotFound(new { error = $"Unknown module '{key}'" });

        var draft = await _draftManager.LoadDraftAsync(HttpContext.GetSessionId(), key) ?? module.DefaultState();

        var dto = new ModuleDto(
            Key: module.Key,
            Label: module.Label,
            Client: module.Client,
            Available: module.Available,
            Environments: module.Environments.Values.Select(e => new EnvironmentDto(
                Key: e.Key,
                Environment: e.Environment,
                Description: e.Description,
                Accent: e.Accent,
                Cue: e.Cue,
                Icon: e.Icon,
                RouteLabel: e.RouteLabel,
                VisualUrl: e.VisualUrl,
                VisualAlt: e.VisualAlt,
                Available: e.Available,
                StatusLabel: e.StatusLabel,
                HasApiUrl: !string.IsNullOrWhiteSpace(e.ApiUrl),
                HasCancelUrl: !string.IsNullOrWhiteSpace(e.CancelUrl),
                IsDefault: e.IsDefault
            )).ToList(),
            Capabilities: ToDto(module.Capabilities)
        );

        return Ok(new { module = dto, state = draft });
    }
}
