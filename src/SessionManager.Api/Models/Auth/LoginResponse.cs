namespace SessionManager.Api.Models.Auth;

public record LoginResponse(
    bool Success,
    string? SessionKey = null,
    UserInfo? User = null,
    string? Error = null
);
