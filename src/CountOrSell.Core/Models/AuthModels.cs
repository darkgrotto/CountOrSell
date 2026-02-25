using System.Text.Json.Serialization;

namespace CountOrSell.Core.Models;

public class RegisterRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

public class LoginRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("user")]
    public UserInfo User { get; set; } = new();
}

public class RefreshRequest
{
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class UserInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("isAdmin")]
    public bool IsAdmin { get; set; }
}

public class AdminUserInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("isAdmin")]
    public bool IsAdmin { get; set; }

    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("lastLoginAt")]
    public DateTime? LastLoginAt { get; set; }
}

public class AppSettingsInfo
{
    [JsonPropertyName("registrationsEnabled")]
    public bool RegistrationsEnabled { get; set; }
}

public class AdminUpdateUserRequest
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("isAdmin")]
    public bool? IsAdmin { get; set; }

    [JsonPropertyName("isDisabled")]
    public bool? IsDisabled { get; set; }
}

public class UpdateDisplayNameRequest
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [JsonPropertyName("currentPassword")]
    public string CurrentPassword { get; set; } = string.Empty;

    [JsonPropertyName("newPassword")]
    public string NewPassword { get; set; } = string.Empty;
}

public class AdminStatusInfo
{
    // Users
    [JsonPropertyName("totalUsers")]
    public int TotalUsers { get; set; }

    [JsonPropertyName("activeUsers")]
    public int ActiveUsers { get; set; }

    [JsonPropertyName("disabledUsers")]
    public int DisabledUsers { get; set; }

    [JsonPropertyName("adminUsers")]
    public int AdminUsers { get; set; }

    // Card data
    [JsonPropertyName("totalSets")]
    public int TotalSets { get; set; }

    [JsonPropertyName("totalCards")]
    public int TotalCards { get; set; }

    [JsonPropertyName("lastCardSyncedAt")]
    public DateTime? LastCardSyncedAt { get; set; }

    // Images
    [JsonPropertyName("cardsWithImages")]
    public int CardsWithImages { get; set; }

    // Collection activity (aggregated across all users)
    [JsonPropertyName("totalOwnershipRecords")]
    public int TotalOwnershipRecords { get; set; }

    [JsonPropertyName("totalOwnedCopies")]
    public int TotalOwnedCopies { get; set; }

    [JsonPropertyName("totalUniqueCardsOwned")]
    public int TotalUniqueCardsOwned { get; set; }

    [JsonPropertyName("reserveListCardsOwned")]
    public int ReserveListCardsOwned { get; set; }

    [JsonPropertyName("totalBoostersDefined")]
    public int TotalBoostersDefined { get; set; }

    [JsonPropertyName("totalBoostersOwned")]
    public int TotalBoostersOwned { get; set; }
}
