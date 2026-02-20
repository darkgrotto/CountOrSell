using Microsoft.AspNetCore.Mvc;
using MtgHelper.Core.Services;

namespace MtgHelper.Api.Controllers;

[ApiController]
[Route("api/images")]
public class ImagesController : ControllerBase
{
    private readonly ICardDataService _cardDataService;

    public ImagesController(ICardDataService cardDataService)
    {
        _cardDataService = cardDataService;
    }

    [HttpGet("{cardId}")]
    public async Task<IActionResult> GetImage(string cardId)
    {
        var imagePath = await _cardDataService.GetCardImagePathAsync(cardId);
        if (imagePath == null)
            return NotFound();

        var bytes = await System.IO.File.ReadAllBytesAsync(imagePath);
        return File(bytes, "image/jpeg");
    }
}
