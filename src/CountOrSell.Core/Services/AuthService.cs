using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using CountOrSell.Core.Data;
using CountOrSell.Core.Entities;
using CountOrSell.Core.Models;

namespace CountOrSell.Core.Services;

public interface IAuthService
{
    Task<User?> RegisterAsync(string username, string password, string? displayName);
    Task<User?> ValidateCredentialsAsync(string username, string password);
    Task<RefreshToken> CreateRefreshTokenAsync(string userId);
    Task<RefreshToken?> ValidateRefreshTokenAsync(string token);
    Task RevokeRefreshTokenAsync(string token);
    Task UpdateLastLoginAsync(string userId);
    Task<User?> UpdateDisplayNameAsync(string userId, string displayName);
    Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
}

public class AuthService : IAuthService
{
    private readonly CountOrSellDbContext _db;

    public AuthService(CountOrSellDbContext db)
    {
        _db = db;
    }

    public async Task<User?> RegisterAsync(string username, string password, string? displayName)
    {
        var exists = await _db.Users.AnyAsync(u => u.Username == username.ToLowerInvariant());
        if (exists) return null;

        var user = new User
        {
            Username = username.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = displayName ?? username,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<User?> ValidateCredentialsAsync(string username, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username.ToLowerInvariant());
        if (user == null) return null;

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? user : null;
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(string userId)
    {
        var token = new RefreshToken
        {
            UserId = userId,
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };

        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync();
        return token;
    }

    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token)
    {
        var refreshToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow);
        return refreshToken;
    }

    public async Task RevokeRefreshTokenAsync(string token)
    {
        var refreshToken = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token);
        if (refreshToken != null)
        {
            refreshToken.IsRevoked = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task UpdateLastLoginAsync(string userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<User?> UpdateDisplayNameAsync(string userId, string displayName)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return null;
        user.DisplayName = displayName.Trim();
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            throw new InvalidOperationException("Current password is incorrect");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return true;
    }
}
