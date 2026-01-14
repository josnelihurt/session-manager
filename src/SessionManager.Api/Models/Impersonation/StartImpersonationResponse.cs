namespace SessionManager.Api.Models.Impersonation;

public record StartImpersonationResponse(
    Guid ImpersonationId,
    string TargetUsername,
    string TargetEmail,
    DateTime ExpiresAt,
    string Message
);
