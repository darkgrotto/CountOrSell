using Microsoft.AspNetCore.Mvc;
using MtgHelper.Core.Models;
using MtgHelper.Core.Services;

namespace MtgHelper.Api.Controllers;

[ApiController]
[Route("api/labels")]
public class LabelsController : ControllerBase
{
    private readonly ICardDataService _cardDataService;
    private readonly ILabelService _labelService;

    public LabelsController(ICardDataService cardDataService, ILabelService labelService)
    {
        _cardDataService = cardDataService;
        _labelService = labelService;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] LabelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SetCode))
            return BadRequest(new { error = "Set code is required" });

        var set = await _cardDataService.GetSetAsync(request.SetCode);
        if (set == null)
            return NotFound(new { error = $"Set '{request.SetCode}' not found" });

        var pdf = _labelService.GenerateLabel(set, request.BoxType);
        return File(pdf, "application/pdf", $"label-{request.SetCode}.pdf");
    }
}
