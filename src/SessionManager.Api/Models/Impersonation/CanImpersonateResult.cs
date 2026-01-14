namespace SessionManager.Api.Models.Impersonation;

public record CanImpersonateResult(
    bool CanImpersonate,
    string? Reason
);
