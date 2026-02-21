using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountOrSell.Core.Models;
using CountOrSell.Core.Services;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Route("api/slabbed")]
[Authorize]
public class SlabbedCardsController : ControllerBase
{
    private readonly ISlabbedCardService _slabbedCardService;

    public SlabbedCardsController(ISlabbedCardService slabbedCardService)
    {
        _slabbedCardService = slabbedCardService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var slabs = await _slabbedCardService.GetAllAsync(UserId);
        return Ok(slabs.Select(s => new
        {
            s.Id,
            s.ScryfallCardId,
            s.CardName,
            s.SetCode,
            s.SetName,
            s.CollectorNumber,
            s.CardVariant,
            s.GradingCompany,
            s.Grade,
            s.CertificationNumber,
            s.PurchaseDate,
            s.PurchasedFrom,
            s.PurchaseCost,
            s.Notes,
            s.CreatedAt
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] SlabbedCardRequest request)
    {
        var slab = await _slabbedCardService.AddAsync(UserId, request);
        return Ok(new
        {
            slab.Id,
            slab.ScryfallCardId,
            slab.CardName,
            slab.SetCode,
            slab.SetName,
            slab.CollectorNumber,
            slab.CardVariant,
            slab.GradingCompany,
            slab.Grade,
            slab.CertificationNumber,
            slab.PurchaseDate,
            slab.PurchasedFrom,
            slab.PurchaseCost,
            slab.Notes,
            slab.CreatedAt
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SlabbedCardRequest request)
    {
        var slab = await _slabbedCardService.UpdateAsync(UserId, id, request);
        if (slab == null) return NotFound();

        return Ok(new
        {
            slab.Id,
            slab.ScryfallCardId,
            slab.CardName,
            slab.SetCode,
            slab.SetName,
            slab.CollectorNumber,
            slab.CardVariant,
            slab.GradingCompany,
            slab.Grade,
            slab.CertificationNumber,
            slab.PurchaseDate,
            slab.PurchasedFrom,
            slab.PurchaseCost,
            slab.Notes,
            slab.CreatedAt
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _slabbedCardService.DeleteAsync(UserId, id);
        return deleted ? Ok() : NotFound();
    }
}
