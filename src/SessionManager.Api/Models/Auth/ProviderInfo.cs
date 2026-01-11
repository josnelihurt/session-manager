namespace SessionManager.Api.Models.Auth;

public record ProviderInfo(
    string Name,
    string DisplayName,
    string? IconUrl
);
