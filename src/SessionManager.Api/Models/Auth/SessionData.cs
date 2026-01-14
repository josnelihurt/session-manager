namespace SessionManager.Api.Models.Auth;

public record SessionData(
    Guid UserId,
    string Username,
    string Email,
    bool IsSuperAdmin,
    DateTime ExpiresAt,
    // Impersonation fields
    bool IsImpersonated = false,
    ImpersonatorInfo? Impersonator = null,
    Guid? ImpersonationId = null,
    DateTime? ImpersonationExpiresAt = null
);

public record ImpersonatorInfo(
    Guid UserId,
    string Username,
    string OriginalSessionKey
);
