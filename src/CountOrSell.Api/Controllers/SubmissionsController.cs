using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CountOrSell.Core.Data;
using CountOrSell.Core.Entities;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Route("api/submissions")]
[Authorize]
public class SubmissionsController : ControllerBase
{
    private readonly CountOrSellDbContext _db;

    public SubmissionsController(CountOrSellDbContext db)
    {
        _db = db;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string Username => User.FindFirstValue(ClaimTypes.Name)!;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSubmissionRequest request)
    {
        var submission = new UserSubmission
        {
            UserId = UserId,
            Username = Username,
            SubmittedAt = DateTime.UtcNow,
            Status = "Pending",
            Items = request.Items.Select(i => new UserSubmissionItem
            {
                ChangeType = i.ChangeType,
                EntityType = i.EntityType,
                EntityId = i.EntityId,
                DataJson = i.DataJson is not null ? JsonSerializer.Serialize(i.DataJson) : null,
                Status = "Pending"
            }).ToList()
        };

        _db.UserSubmissions.Add(submission);
        await _db.SaveChangesAsync();

        return Ok(new { submission.Id, submission.Status, itemCount = submission.Items.Count });
    }

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var submissions = await _db.UserSubmissions
            .Where(s => s.UserId == UserId)
            .Include(s => s.Items)
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => new
            {
                s.Id,
                s.SubmittedAt,
                s.Status,
                s.ReviewNotes,
                s.ReviewedAt,
                ItemCount = s.Items.Count
            })
            .ToListAsync();

        return Ok(submissions);
    }
}

public class CreateSubmissionRequest
{
    public List<SubmissionItemRequest> Items { get; set; } = new();
}

public class SubmissionItemRequest
{
    public string ChangeType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public object? DataJson { get; set; }
}
