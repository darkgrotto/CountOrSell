using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountOrSell.Core.Models;
using CountOrSell.Core.Services;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Route("api/collection")]
[Authorize]
public class CollectionController : ControllerBase
{
    private readonly ICollectionService _collectionService;

    public CollectionController(ICollectionService collectionService)
    {
        _collectionService = collectionService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private CollectionFilter ParseFilter() => new CollectionFilter
    {
        Rarity  = Request.Query["rarity"].FirstOrDefault()  ?? "all",
        Type    = Request.Query["type"].FirstOrDefault()    ?? "all",
        Color   = Request.Query["color"].FirstOrDefault()   ?? "all",
        Variant = Request.Query["variant"].FirstOrDefault() ?? "all",
        SetCode = Request.Query["setCode"].FirstOrDefault() ?? "all",
    };

    [HttpGet]
    public async Task<IActionResult> GetCollection()
    {
        var filter = ParseFilter();
        var cards = await _collectionService.GetAllOwnedCardsAsync(UserId, filter);
        return Ok(cards);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetCollectionSummary()
    {
        var summary = await _collectionService.GetCollectionSummaryAsync(UserId);
        return Ok(summary);
    }
}
