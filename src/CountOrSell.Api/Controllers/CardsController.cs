using Microsoft.AspNetCore.Mvc;
using CountOrSell.Core.Services;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Route("api/sets/{setCode}/cards")]
public class CardsController : ControllerBase
{
    private readonly ICardDataService _cardDataService;

    public CardsController(ICardDataService cardDataService)
    {
        _cardDataService = cardDataService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCards(string setCode)
    {
        var cards = await _cardDataService.GetCardsForSetAsync(setCode);
        return Ok(cards);
    }
}
