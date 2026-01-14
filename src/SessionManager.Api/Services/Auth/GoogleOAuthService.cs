using System.Text.Json;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Models.Auth;

namespace SessionManager.Api.Services.Auth;

public class GoogleOAuthService : IGoogleOAuthService
{
    private readonly GoogleOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleOAuthService> _logger;

    public GoogleOAuthService(
        IOptions<GoogleOptions> options,
        HttpClient httpClient,
        ILogger<GoogleOAuthService> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public string GetAuthorizationUrl(string state, string? invitationToken = null)
    {
        var redirectUri = Uri.EscapeDataString(_options.RedirectUri);
        var scope = Uri.EscapeDataString("openid email profile");

        // Include invitation token in state if provided
        var fullState = invitationToken != null
            ? $"{state}|{invitationToken}"
            : state;

        return $"{SessionManagerConstants.Urls.GoogleAuthUrl}?" +
               $"client_id={_options.ClientId}&" +
               $"redirect_uri={redirectUri}&" +
               $"response_type=code&" +
               $"scope={scope}&" +
               $"state={Uri.EscapeDataString(fullState)}&" +
               $"access_type=offline&" +
               $"prompt=consent";
    }

    public async Task<GoogleTokenResponse?> ExchangeCodeAsync(string code)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["redirect_uri"] = _options.RedirectUri,
                ["grant_type"] = "authorization_code"
            });

            var response = await _httpClient.PostAsync(
                SessionManagerConstants.Urls.GoogleTokenUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Google token exchange failed: {Error}", error);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

            return new GoogleTokenResponse(
                AccessToken: tokenData.GetProperty("access_token").GetString()!,
                IdToken: tokenData.GetProperty("id_token").GetString()!,
                RefreshToken: tokenData.TryGetProperty("refresh_token", out var rt)
                    ? rt.GetString()! : "",
                ExpiresIn: tokenData.GetProperty("expires_in").GetInt32(),
                TokenType: tokenData.GetProperty("token_type").GetString()!
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exchange Google auth code");
            return null;
        }
    }

    public async Task<GoogleUserInfo?> GetUserInfoAsync(string accessToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                SessionManagerConstants.Urls.GoogleUserInfoUrl);
            request.Headers.Authorization = new(SessionManagerConstants.HttpHeaders.BearerScheme, accessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            return new GoogleUserInfo(
                Id: data.GetProperty("id").GetString()!,
                Email: data.GetProperty("email").GetString()!,
                EmailVerified: data.GetProperty("verified_email").GetBoolean(),
                Name: data.GetProperty("name").GetString()!,
                Picture: data.TryGetProperty("picture", out var pic)
                    ? pic.GetString() : null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Google user info");
            return null;
        }
    }
}
