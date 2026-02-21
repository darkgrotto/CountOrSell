using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountOrSell.Core.Models;
using CountOrSell.Core.Services;

namespace CountOrSell.Api.Controllers;

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

    [HttpGet("tags")]
    public IActionResult GetKnownTags()
    {
        return Ok(KnownSetTags.All.OrderBy(t => t));
    }

    [Authorize]
    [HttpPut("{setCode}/tags/{tag}")]
    public async Task<IActionResult> AddTag(string setCode, string tag)
    {
        if (!KnownSetTags.All.Contains(tag))
            return BadRequest(new { error = $"Unknown tag '{tag}'. Valid tags: {string.Join(", ", KnownSetTags.All.OrderBy(t => t))}" });

        var ok = await _cardDataService.AddTagAsync(setCode, tag);
        if (!ok)
            return NotFound(new { error = $"Set '{setCode}' not found" });

        return NoContent();
    }

    [Authorize]
    [HttpDelete("{setCode}/tags/{tag}")]
    public async Task<IActionResult> RemoveTag(string setCode, string tag)
    {
        var ok = await _cardDataService.RemoveTagAsync(setCode, tag);
        if (!ok)
            return NotFound(new { error = $"Tag '{tag}' not found on set '{setCode}'" });

        return NoContent();
    }
}
