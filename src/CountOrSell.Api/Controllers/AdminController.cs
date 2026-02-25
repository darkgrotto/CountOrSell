using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CountOrSell.Core.Data;
using CountOrSell.Core.Models;
using CountOrSell.Core.Services;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly CountOrSellDbContext _db;

    public AdminController(IAuthService authService, CountOrSellDbContext db)
    {
        _authService = authService;
        _db = db;
    }

    private bool IsAdmin() => User.FindFirstValue("IsAdmin") == "true";

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers()
    {
        if (!IsAdmin()) return Forbid();

        var users = await _authService.GetAllUsersAsync();
        return Ok(users.Select(u => new AdminUserInfo
        {
            Id = u.Id,
            Username = u.Username,
            DisplayName = u.DisplayName,
            IsAdmin = u.IsAdmin,
            IsDisabled = u.IsDisabled,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt,
        }));
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] AdminUpdateUserRequest request)
    {
        if (!IsAdmin()) return Forbid();

        var requestingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (id == requestingUserId)
        {
            if (request.IsAdmin == false)
                return BadRequest(new { error = "Cannot demote your own account" });
            if (request.IsDisabled == true)
                return BadRequest(new { error = "Cannot disable your own account" });
        }

        var user = await _authService.AdminUpdateUserAsync(id, request.DisplayName, request.IsAdmin, request.IsDisabled);
        if (user == null) return NotFound();

        return Ok(new AdminUserInfo
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            IsAdmin = user.IsAdmin,
            IsDisabled = user.IsDisabled,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
        });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] AppSettingsInfo request)
    {
        if (!IsAdmin()) return Forbid();

        var settings = await _db.AppSettings.FindAsync(1);
        if (settings == null) return NotFound();

        settings.RegistrationsEnabled = request.RegistrationsEnabled;
        await _db.SaveChangesAsync();

        return Ok(new AppSettingsInfo { RegistrationsEnabled = settings.RegistrationsEnabled });
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        if (!IsAdmin()) return Forbid();

        var users = await _db.Users.ToListAsync();
        var totalCards = await _db.CachedCards.CountAsync();
        var lastSync = totalCards > 0
            ? await _db.CachedCards.MaxAsync(c => c.LastSyncedAt)
            : (DateTime?)null;

        var ownerships = await _db.CardOwnerships
            .Where(o => o.Quantity > 0)
            .Select(o => new { o.ScryfallCardId, o.Quantity })
            .ToListAsync();

        var rlOwned = await _db.ReserveListCardOwnerships
            .Where(r => r.Owned)
            .Select(r => r.ScryfallCardId)
            .Distinct()
            .CountAsync();

        return Ok(new AdminStatusInfo
        {
            TotalUsers = users.Count,
            ActiveUsers = users.Count(u => !u.IsDisabled),
            DisabledUsers = users.Count(u => u.IsDisabled),
            AdminUsers = users.Count(u => u.IsAdmin),
            TotalSets = await _db.CachedSets.CountAsync(),
            TotalCards = totalCards,
            LastCardSyncedAt = lastSync,
            CardsWithImages = await _db.CachedCards.CountAsync(c => c.LocalImagePath != null && c.LocalImagePath != ""),
            TotalOwnershipRecords = ownerships.Count,
            TotalOwnedCopies = ownerships.Sum(o => o.Quantity),
            TotalUniqueCardsOwned = ownerships.Select(o => o.ScryfallCardId).Distinct().Count(),
            ReserveListCardsOwned = rlOwned,
            TotalBoostersDefined = await _db.BoosterDefinitions.CountAsync(),
            TotalBoostersOwned = await _db.BoosterDefinitions.CountAsync(b => b.Owned),
        });
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        if (!IsAdmin()) return Forbid();

        var requestingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (id == requestingUserId)
            return BadRequest(new { error = "Cannot delete your own account" });

        var deleted = await _authService.AdminDeleteUserAsync(id);
        if (!deleted) return NotFound();

        return NoContent();
    }
}
