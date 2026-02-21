using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountOrSell.Core.Models;
using CountOrSell.Core.Services;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Route("api/reservelist")]
[Authorize]
public class ReserveListController : ControllerBase
{
    private readonly ICardDataService _cardDataService;
    private readonly ICollectionService _collectionService;

    public ReserveListController(ICardDataService cardDataService, ICollectionService collectionService)
    {
        _cardDataService = cardDataService;
        _collectionService = collectionService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> GetReserveList()
    {
        var cards = await _cardDataService.GetReserveListCardsAsync();
        var ownerships = await _collectionService.GetAllReserveListOwnershipsAsync(UserId);
        var ownedMap = ownerships.ToDictionary(o => o.ScryfallCardId, o => o.Owned);

        var response = cards.Select(c => new ReserveListCardResponse
        {
            Id = c.Id,
            Name = c.Name,
            Set = c.Set,
            SetName = c.SetName,
            CollectorNumber = c.CollectorNumber,
            Rarity = c.Rarity,
            TypeLine = c.TypeLine,
            ManaCost = c.ManaCost,
            ImageUris = c.ImageUris,
            CardFaces = c.CardFaces,
            Prices = c.Prices,
            ScryfallUri = c.ScryfallUri,
            Owned = ownedMap.TryGetValue(c.Id, out var owned) && owned
        });

        return Ok(response);
    }

    [HttpPatch("{scryfallCardId}/owned")]
    public async Task<IActionResult> SetOwned(string scryfallCardId, [FromBody] ReserveListOwnedRequest request)
    {
        var ownership = await _collectionService.SetReserveListOwnedAsync(
            UserId, scryfallCardId, request.CardName, request.SetCode, request.Owned);
        return Ok(new { ownership.ScryfallCardId, ownership.Owned });
    }

    [HttpGet("set/{setCode}")]
    public async Task<IActionResult> GetReserveListForSet(string setCode)
    {
        var ids = await _collectionService.GetReserveListCardIdsForSetAsync(UserId, setCode);
        return Ok(ids);
    }
}
