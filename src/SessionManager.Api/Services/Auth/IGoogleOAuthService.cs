using SessionManager.Api.Models.Auth;

namespace SessionManager.Api.Services.Auth;

public interface IGoogleOAuthService
{
    string GetAuthorizationUrl(string state, string? invitationToken = null);
    Task<GoogleTokenResponse?> ExchangeCodeAsync(string code);
    Task<GoogleUserInfo?> GetUserInfoAsync(string accessToken);
}

public record GoogleTokenResponse(
    string AccessToken,
    string IdToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType
);

public record GoogleUserInfo(
    string Id,
    string Email,
    bool EmailVerified,
    string Name,
    string? Picture
);
