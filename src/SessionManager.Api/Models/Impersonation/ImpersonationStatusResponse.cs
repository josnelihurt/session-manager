namespace SessionManager.Api.Models.Impersonation;

public record ImpersonationStatusResponse(
    bool IsImpersonating,
    Guid? ImpersonationId,
    string? TargetUsername,
    string? TargetEmail,
    DateTime? ExpiresAt,
    int? RemainingMinutes,
    string? OriginalUsername
);
