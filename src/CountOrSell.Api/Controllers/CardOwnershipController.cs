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
    public async Task<IActionResult> GetOwnedCardIds(string setCode)
    {
        var ids = await _collectionService.GetOwnedCardIdsForSetAsync(UserId, setCode);
        return Ok(ids);
    }

    [HttpPatch("api/cards/{scryfallCardId}/owned")]
    public async Task<IActionResult> SetCardOwned(string scryfallCardId, [FromBody] CardOwnedRequest request)
    {
        var ownership = await _collectionService.SetCardOwnedAsync(
            UserId, scryfallCardId, request.CardName, request.SetCode, request.CollectorNumber, request.Owned);
        return Ok(new { ownership.ScryfallCardId, ownership.Owned });
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
