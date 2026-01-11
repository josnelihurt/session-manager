namespace SessionManager.Api.Models.Auth;

public record SessionData(
    Guid UserId,
    string Username,
    string Email,
    bool IsSuperAdmin,
    DateTime ExpiresAt
);
