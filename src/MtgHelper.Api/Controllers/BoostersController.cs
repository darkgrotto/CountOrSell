using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MtgHelper.Core.Models;
using MtgHelper.Core.Services;

namespace MtgHelper.Api.Controllers;

[ApiController]
[Route("api/boosters")]
[Authorize]
public class BoostersController : ControllerBase
{
    private readonly ICollectionService _collectionService;

    public BoostersController(ICollectionService collectionService)
    {
        _collectionService = collectionService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var boosters = await _collectionService.GetAllBoostersAsync(UserId);
        return Ok(boosters.Select(b => new BoosterResponse
        {
            Id = b.Id,
            SetCode = b.SetCode,
            BoosterType = b.BoosterType,
            ArtVariant = b.ArtVariant,
            ImageUrl = b.ImageUrl,
            Owned = b.Owned
        }));
    }

    [HttpGet("set/{setCode}")]
    public async Task<IActionResult> GetForSet(string setCode)
    {
        var boosters = await _collectionService.GetBoostersForSetAsync(UserId, setCode);
        return Ok(boosters.Select(b => new BoosterResponse
        {
            Id = b.Id,
            SetCode = b.SetCode,
            BoosterType = b.BoosterType,
            ArtVariant = b.ArtVariant,
            ImageUrl = b.ImageUrl,
            Owned = b.Owned
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BoosterRequest request)
    {
        var booster = await _collectionService.UpsertBoosterAsync(
            UserId, request.SetCode, request.BoosterType, request.ArtVariant, request.ImageUrl);

        return Ok(new BoosterResponse
        {
            Id = booster.Id,
            SetCode = booster.SetCode,
            BoosterType = booster.BoosterType,
            ArtVariant = booster.ArtVariant,
            ImageUrl = booster.ImageUrl,
            Owned = booster.Owned
        });
    }

    [HttpPatch("{id}/owned")]
    public async Task<IActionResult> SetOwned(int id, [FromBody] BoosterOwnedRequest request)
    {
        var booster = await _collectionService.SetBoosterOwnedAsync(UserId, id, request.Owned);
        if (booster == null)
            return NotFound();

        return Ok(new BoosterResponse
        {
            Id = booster.Id,
            SetCode = booster.SetCode,
            BoosterType = booster.BoosterType,
            ArtVariant = booster.ArtVariant,
            ImageUrl = booster.ImageUrl,
            Owned = booster.Owned
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _collectionService.DeleteBoosterAsync(UserId, id);
        return deleted ? Ok() : NotFound();
    }
}
