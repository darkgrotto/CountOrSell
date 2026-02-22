using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using CountOrSell.Core.Entities;
using CountOrSell.Core.Models;
using CountOrSell.Core.Services;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _config;
    private readonly CountOrSell.Core.Data.CountOrSellDbContext _db;

    public AuthController(IAuthService authService, IConfiguration config, CountOrSell.Core.Data.CountOrSellDbContext db)
    {
        _authService = authService;
        _config = config;
        _db = db;
    }

    [HttpGet("registration-status")]
    public async Task<IActionResult> GetRegistrationStatus()
    {
        var settings = await _db.AppSettings.FindAsync(1);
        return Ok(new AppSettingsInfo { RegistrationsEnabled = settings?.RegistrationsEnabled ?? true });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var settings = await _db.AppSettings.FindAsync(1);
        if (settings?.RegistrationsEnabled == false)
            return BadRequest(new { error = "Registrations are currently disabled" });

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password are required" });

        if (request.Password.Length < 15)
            return BadRequest(new { error = "Password must be at least 15 characters" });

        var user = await _authService.RegisterAsync(request.Username, request.Password, request.DisplayName);
        if (user == null)
            return Conflict(new { error = "Username already exists" });

        var response = await GenerateAuthResponse(user.Id, user.Username, user.DisplayName, user.IsAdmin);
        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        User? user;
        try
        {
            user = await _authService.ValidateCredentialsAsync(request.Username, request.Password);
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        if (user == null)
            return Unauthorized(new { error = "Invalid username or password" });

        await _authService.UpdateLastLoginAsync(user.Id);
        var response = await GenerateAuthResponse(user.Id, user.Username, user.DisplayName, user.IsAdmin);
        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var refreshToken = await _authService.ValidateRefreshTokenAsync(request.RefreshToken);
        if (refreshToken == null)
            return Unauthorized(new { error = "Invalid or expired refresh token" });

        await _authService.RevokeRefreshTokenAsync(request.RefreshToken);

        var user = await _db.Users.FindAsync(refreshToken.UserId);
        if (user == null)
            return Unauthorized(new { error = "User not found" });

        if (user.IsDisabled)
            return Unauthorized(new { error = "Account has been disabled. Contact an administrator." });

        var response = await GenerateAuthResponse(user.Id, user.Username, user.DisplayName, user.IsAdmin);
        return Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        return Ok(new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            IsAdmin = user.IsAdmin,
        });
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateDisplayNameRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest(new { error = "Display name cannot be blank" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _authService.UpdateDisplayNameAsync(userId, request.DisplayName);
        if (user == null) return NotFound();

        return Ok(new UserInfo { Id = user.Id, Username = user.Username, DisplayName = user.DisplayName, IsAdmin = user.IsAdmin });
    }

    [Authorize]
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { error = "All password fields are required" });
        if (request.NewPassword.Length < 15)
            return BadRequest(new { error = "Password must be at least 15 characters" });
        if (request.CurrentPassword == request.NewPassword)
            return BadRequest(new { error = "New password must be different from current password" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
            return Ok(new { message = "Password changed successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<AuthResponse> GenerateAuthResponse(string userId, string username, string? displayName, bool isAdmin = false)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiration = DateTime.UtcNow.AddMinutes(
            int.Parse(_config["Jwt:AccessTokenExpirationMinutes"] ?? "60"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, username),
            new Claim("DisplayName", displayName ?? username),
            new Claim("IsAdmin", isAdmin.ToString().ToLower())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiration,
            signingCredentials: creds
        );

        var refreshToken = await _authService.CreateRefreshTokenAsync(userId);

        return new AuthResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken = refreshToken.Token,
            ExpiresAt = expiration,
            User = new UserInfo
            {
                Id = userId,
                Username = username,
                DisplayName = displayName,
                IsAdmin = isAdmin,
            }
        };
    }
}
