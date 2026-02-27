using Microsoft.AspNetCore.Mvc;
using CountOrSell.Api.Services;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Route("api")]
public class ConfigController : ControllerBase
{
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var sphApiUrl = Environment.GetEnvironmentVariable("SPH_API_URL");
        var demoMode  = string.Equals(
            Environment.GetEnvironmentVariable("COS_DEMO_MODE"), "true",
            StringComparison.OrdinalIgnoreCase);

        string? demoResetAt = null;
        if (demoMode && DemoResetService.NextResetAt.HasValue)
            demoResetAt = DemoResetService.NextResetAt.Value.ToString("O");

        return Ok(new
        {
            sphEnabled = !string.IsNullOrEmpty(sphApiUrl),
            sphBaseUrl = "/sph",
            demoMode,
            demoResetAt,
        });
    }
}
