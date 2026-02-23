using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountOrSell.Core.Services;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Route("api/import")]
[Authorize]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public ImportController(IImportService importService)
    {
        _importService = importService;
    }

    [HttpPost("collection")]
    public async Task<IActionResult> ImportCollection(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        using var stream = file.OpenReadStream();
        var result = await _importService.ImportCollectionAsync(UserId, stream, file.FileName);

        if (result.Error != null)
            return BadRequest(new { error = result.Error });

        return Ok(result);
    }
}
