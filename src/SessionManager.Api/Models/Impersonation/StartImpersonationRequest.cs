namespace SessionManager.Api.Models.Impersonation;

public record StartImpersonationRequest(
    string Reason,
    int? DurationMinutes
);
