using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountOrSell.Core.Services;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly IExportService _exportService;

    public ExportController(IExportService exportService)
    {
        _exportService = exportService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("cards/csv")]
    public async Task<IActionResult> ExportCardsCsv()
    {
        var data = await _exportService.ExportOwnedCardsAsCsvAsync(UserId);
        return File(data, "text/csv", "owned-cards.csv");
    }

    [HttpGet("cards/xml")]
    public async Task<IActionResult> ExportCardsXml()
    {
        var data = await _exportService.ExportOwnedCardsAsXmlAsync(UserId);
        return File(data, "application/xml", "owned-cards.xml");
    }

    [HttpGet("boosters/csv")]
    public async Task<IActionResult> ExportBoostersCsv()
    {
        var data = await _exportService.ExportBoostersAsCsvAsync(UserId);
        return File(data, "text/csv", "boosters.csv");
    }

    [HttpGet("reservelist/csv")]
    public async Task<IActionResult> ExportReserveListCsv()
    {
        var data = await _exportService.ExportReserveListAsCsvAsync(UserId);
        return File(data, "text/csv", "reserve-list.csv");
    }
}
