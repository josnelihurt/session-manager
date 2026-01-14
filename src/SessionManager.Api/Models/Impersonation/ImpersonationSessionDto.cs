namespace SessionManager.Api.Models.Impersonation;

public record ImpersonationSessionDto(
    Guid Id,
    string ImpersonatorUsername,
    string TargetUsername,
    string? Reason,
    DateTime StartedAt,
    DateTime ExpiresAt,
    int RemainingMinutes,
    string? IpAddress
);
