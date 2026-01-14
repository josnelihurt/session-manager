namespace SessionManager.Api.Models.Impersonation;

public record ImpersonationResult(
    bool Success,
    string? Error,
    string? NewSessionKey,
    Guid? ImpersonationId,
    DateTime? ExpiresAt
);
