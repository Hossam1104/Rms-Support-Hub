using Microsoft.AspNetCore.Mvc;
using OnlineOrderTool.Api.Middleware;
using OnlineOrderTool.Core.DTOs;
using OnlineOrderTool.Core.Modules;
using OnlineOrderTool.Core.Services;

namespace OnlineOrderTool.Api.Controllers;

[ApiController]
[Route("api/modules")]
public class ModuleController : ControllerBase
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IDraftManager _draftManager;

    public ModuleController(IModuleRegistry moduleRegistry, IDraftManager draftManager)
    {
        _moduleRegistry = moduleRegistry;
        _draftManager = draftManager;
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
                HasCancelUrl: !string.IsNullOrWhiteSpace(e.CancelUrl)
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
        HasDeliveryFields: c.HasDeliveryFields
    );

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
                HasCancelUrl: !string.IsNullOrWhiteSpace(e.CancelUrl)
            )).ToList(),
            Capabilities: ToDto(module.Capabilities)
        );

        return Ok(new { module = dto, state = draft });
    }
}
