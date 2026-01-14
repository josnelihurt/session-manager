namespace SessionManager.Api.Models.Auth;

public record UserInfo(
    Guid Id,
    string Username,
    string Email,
    bool IsSuperAdmin,
    string Provider,
    bool CanImpersonate = false
);

public record LoginSuccessResponse(UserInfo User);
