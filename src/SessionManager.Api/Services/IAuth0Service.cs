using SessionManager.Api.Models.Auth;

namespace SessionManager.Api.Services;

public interface IAuth0Service
{
    string GetAuthorizationUrl(string state, string? invitationToken = null, bool forceLogin = false, bool isRegistration = false, string? emailHint = null);
    Task<(string? IdToken, string? AccessToken)> ExchangeCodeForTokensAsync(string code);
    Task<Auth0UserInfo?> GetUserInfoAsync(string accessToken);
    Task UpdateUserRolesAsync(string auth0UserId, Dictionary<string, List<string>> applicationRoles);
    Task DeleteUserAsync(string auth0UserId);
}

public record Auth0UserInfo(
    string UserId,
    string Email,
    string Name,
    string? Picture
);
