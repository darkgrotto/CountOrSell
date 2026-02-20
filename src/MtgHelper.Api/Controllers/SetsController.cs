using Microsoft.AspNetCore.Mvc;
using MtgHelper.Core.Services;

namespace MtgHelper.Api.Controllers;

[ApiController]
[Route("api/sets")]
public class SetsController : ControllerBase
{
    private readonly ICardDataService _cardDataService;

    public SetsController(ICardDataService cardDataService)
    {
        _cardDataService = cardDataService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSets()
    {
        var sets = await _cardDataService.GetSetsAsync();
        return Ok(sets);
    }

    [HttpGet("{setCode}")]
    public async Task<IActionResult> GetSet(string setCode)
    {
        var set = await _cardDataService.GetSetAsync(setCode);
        if (set == null)
            return NotFound(new { error = $"Set '{setCode}' not found" });

        return Ok(set);
    }
}
