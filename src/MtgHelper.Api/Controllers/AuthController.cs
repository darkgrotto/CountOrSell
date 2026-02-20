using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MtgHelper.Core.Models;
using MtgHelper.Core.Services;

namespace MtgHelper.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _config;

    public AuthController(IAuthService authService, IConfiguration config)
    {
        _authService = authService;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password are required" });

        if (request.Password.Length < 6)
            return BadRequest(new { error = "Password must be at least 6 characters" });

        var user = await _authService.RegisterAsync(request.Username, request.Password, request.DisplayName);
        if (user == null)
            return Conflict(new { error = "Username already exists" });

        var response = await GenerateAuthResponse(user.Id, user.Username, user.DisplayName);
        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _authService.ValidateCredentialsAsync(request.Username, request.Password);
        if (user == null)
            return Unauthorized(new { error = "Invalid username or password" });

        await _authService.UpdateLastLoginAsync(user.Id);
        var response = await GenerateAuthResponse(user.Id, user.Username, user.DisplayName);
        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var refreshToken = await _authService.ValidateRefreshTokenAsync(request.RefreshToken);
        if (refreshToken == null)
            return Unauthorized(new { error = "Invalid or expired refresh token" });

        // Revoke old token and issue new one
        await _authService.RevokeRefreshTokenAsync(request.RefreshToken);

        // Look up user info from the database
        using var scope = HttpContext.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MtgHelper.Core.Data.MtgHelperDbContext>();
        var user = await db.Users.FindAsync(refreshToken.UserId);
        if (user == null)
            return Unauthorized(new { error = "User not found" });

        var response = await GenerateAuthResponse(user.Id, user.Username, user.DisplayName);
        return Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.FindFirstValue(ClaimTypes.Name);
        var displayName = User.FindFirstValue("DisplayName");

        return Ok(new UserInfo
        {
            Id = userId ?? "",
            Username = username ?? "",
            DisplayName = displayName
        });
    }

    private async Task<AuthResponse> GenerateAuthResponse(string userId, string username, string? displayName)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiration = DateTime.UtcNow.AddMinutes(
            int.Parse(_config["Jwt:AccessTokenExpirationMinutes"] ?? "60"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, username),
            new Claim("DisplayName", displayName ?? username)
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
                DisplayName = displayName
            }
        };
    }
}
