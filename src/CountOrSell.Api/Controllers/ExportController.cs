using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountOrSell.Core.Models;
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

    [HttpGet("slabbed/pdf")]
    public async Task<IActionResult> SlabbedPdf()
    {
        var bytes = await _exportService.ExportSlabbedCardsAsPdfAsync(UserId);
        return File(bytes, "application/pdf", "slabbed-collection.pdf");
    }

    private CollectionFilter ParseCollectionFilter() => new CollectionFilter
    {
        Rarity  = Request.Query["rarity"].FirstOrDefault()  ?? "all",
        Type    = Request.Query["type"].FirstOrDefault()    ?? "all",
        Color   = Request.Query["color"].FirstOrDefault()   ?? "all",
        Variant = Request.Query["variant"].FirstOrDefault() ?? "all",
        SetCode = Request.Query["setCode"].FirstOrDefault() ?? "all",
    };

    [HttpGet("collection/summary/csv")]
    public async Task<IActionResult> CollectionSummaryCsv()
    {
        var data = await _exportService.ExportCollectionSummaryAsCsvAsync(UserId);
        return File(data, "text/csv", "collection-summary.csv");
    }

    [HttpGet("collection/summary/xml")]
    public async Task<IActionResult> CollectionSummaryXml()
    {
        var data = await _exportService.ExportCollectionSummaryAsXmlAsync(UserId);
        return File(data, "application/xml", "collection-summary.xml");
    }

    [HttpGet("collection/summary/pdf")]
    public async Task<IActionResult> CollectionSummaryPdf()
    {
        var data = await _exportService.ExportCollectionSummaryAsPdfAsync(UserId);
        return File(data, "application/pdf", "collection-summary.pdf");
    }

    [HttpGet("collection/detailed/csv")]
    public async Task<IActionResult> CollectionDetailedCsv()
    {
        var data = await _exportService.ExportCollectionDetailedAsCsvAsync(UserId, ParseCollectionFilter());
        return File(data, "text/csv", "collection-detailed.csv");
    }

    [HttpGet("collection/detailed/xml")]
    public async Task<IActionResult> CollectionDetailedXml()
    {
        var data = await _exportService.ExportCollectionDetailedAsXmlAsync(UserId, ParseCollectionFilter());
        return File(data, "application/xml", "collection-detailed.xml");
    }

    [HttpGet("collection/detailed/pdf")]
    public async Task<IActionResult> CollectionDetailedPdf()
    {
        var data = await _exportService.ExportCollectionDetailedAsPdfAsync(UserId, ParseCollectionFilter());
        return File(data, "application/pdf", "collection-detailed.pdf");
    }
}
