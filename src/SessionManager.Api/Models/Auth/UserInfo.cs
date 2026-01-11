namespace SessionManager.Api.Models.Auth;

public record UserInfo(
    Guid Id,
    string Username,
    string Email,
    bool IsSuperAdmin,
    string Provider
);

public record LoginSuccessResponse(UserInfo User);
