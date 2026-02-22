using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountOrSell.Core.Models;
using CountOrSell.Core.Services;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IAuthService _authService;

    public AdminController(IAuthService authService)
    {
        _authService = authService;
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
