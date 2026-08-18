using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RmsSupportHub.Api.Configuration;

namespace RmsSupportHub.Api.Controllers;

/// <summary>
/// Local process and runtime-storage checks for IIS/load-balancer probes.
/// These endpoints never call an RMS gateway or database, so they remain
/// useful while the application is starting in an offline Testing package.
/// </summary>
[ApiController]
[Route("api/health")]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly IHostEnvironment _environment;
    private readonly IOptions<SupportHubOptions> _options;

    public HealthController(IHostEnvironment environment, IOptions<SupportHubOptions> options)
    {
        _environment = environment;
        _options = options;
    }

    [HttpGet("live")]
    public ActionResult<object> Live() => Ok(new
    {
        status = "healthy",
        service = "RmsSupportHub.Api"
    });

    [HttpGet("ready")]
    public ActionResult<object> Ready()
    {
        var draftsRoot = Path.Combine(_environment.ContentRootPath, "var", "drafts");

        try
        {
            Directory.CreateDirectory(draftsRoot);
            var probePath = Path.Combine(draftsRoot, $".readiness-{Guid.NewGuid():N}.tmp");
            System.IO.File.WriteAllText(probePath, "ready", Encoding.UTF8);
            System.IO.File.Delete(probePath);

            return Ok(new
            {
                status = "ready",
                deploymentTier = _options.Value.DeploymentTier,
                runtimeStorage = "ready"
            });
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "not_ready",
                runtimeStorage = "unavailable"
            });
        }
    }
}
