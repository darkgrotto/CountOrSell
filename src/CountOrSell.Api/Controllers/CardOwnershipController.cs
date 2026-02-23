using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountOrSell.Core.Models;
using CountOrSell.Core.Services;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Authorize]
public class CardOwnershipController : ControllerBase
{
    private readonly ICollectionService _collectionService;

    public CardOwnershipController(ICollectionService collectionService)
    {
        _collectionService = collectionService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("api/sets/{setCode}/owned-cards")]
    public async Task<IActionResult> GetCardQuantities(string setCode)
    {
        var entries = await _collectionService.GetCardQuantitiesForSetAsync(UserId, setCode);
        return Ok(entries);
    }

    [HttpPut("api/cards/{scryfallCardId}/variant")]
    public async Task<IActionResult> SetCardVariantQuantity(string scryfallCardId, [FromBody] CardVariantQuantityRequest request)
    {
        var ownership = await _collectionService.SetCardVariantQuantityAsync(
            UserId, scryfallCardId, request.Variant, request.Quantity,
            request.CardName, request.SetCode, request.CollectorNumber);
        return Ok(new { ownership.ScryfallCardId, ownership.Variant, ownership.Quantity });
    }

    [HttpPost("api/cards/bulk-owned")]
    public async Task<IActionResult> BulkSetOwned([FromBody] BulkCardOwnedRequest request)
    {
        var cards = request.Cards.Select(c =>
            (c.ScryfallCardId, c.CardName, request.SetCode, c.CollectorNumber)).ToList();

        await _collectionService.BulkSetCardsOwnedAsync(UserId, cards, request.Owned);
        return Ok(new { updated = cards.Count });
    }
}
