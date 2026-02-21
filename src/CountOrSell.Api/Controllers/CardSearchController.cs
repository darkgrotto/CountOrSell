using Microsoft.AspNetCore.Mvc;
using CountOrSell.Core.Services;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Route("api/cards")]
public class CardSearchController : ControllerBase
{
    private readonly ICardDataService _cardDataService;

    public CardSearchController(ICardDataService cardDataService)
    {
        _cardDataService = cardDataService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Array.Empty<object>());

        var results = await _cardDataService.SearchCardsAsync(q, limit);
        return Ok(results);
    }
}
