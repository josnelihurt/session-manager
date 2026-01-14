using System.Text.Json;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Models.Auth;

namespace SessionManager.Api.Services.Auth;

public class Auth0Service : IAuth0Service
{
    private readonly Auth0Options _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<Auth0Service> _logger;

    public Auth0Service(
        IOptions<Auth0Options> options,
        HttpClient httpClient,
        ILogger<Auth0Service> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public string GetAuthorizationUrl(string state, string? invitationToken = null, bool forceLogin = false, bool isRegistration = false, string? emailHint = null)
    {
        // Build state parameter with invitation token if provided
        var fullState = invitationToken != null
            ? $"{invitationToken}|{state}"
            : state;

        var redirectUri = Uri.EscapeDataString(_options.CallbackUrl);
        var scope = Uri.EscapeDataString("openid profile email");

        // Extract domain from full URL (remove protocol if present)
        var domain = _options.Domain.StartsWith("http")
            ? _options.Domain
            : $"https://{_options.Domain}";

        var url = $"{domain}/authorize?" +
               $"client_id={_options.ClientId}&" +
               $"redirect_uri={redirectUri}&" +
               $"response_type=code&" +
               $"scope={scope}&" +
               $"state={Uri.EscapeDataString(fullState)}";

        // Add prompt=login to force re-authentication (required after logout)
        if (forceLogin)
        {
            url += "&prompt=login";
        }

        // Add screen_hint=signup to show sign-up screen directly for registration flow
        if (isRegistration)
        {
            url += "&screen_hint=signup";
        }

        // Add login_hint to pre-fill email from invitation
        if (!string.IsNullOrEmpty(emailHint))
        {
            url += $"&login_hint={Uri.EscapeDataString(emailHint)}";
        }

        return url;
    }

    public async Task<(string? IdToken, string? AccessToken)> ExchangeCodeForTokensAsync(string code)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = _options.CallbackUrl
            });

            var domain = _options.Domain.StartsWith("http")
                ? _options.Domain
                : $"https://{_options.Domain}";

            var response = await _httpClient.PostAsync($"{domain}/oauth/token", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Auth0 token exchange failed: {Error}", error);
                return (null, null);
            }

            var json = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

            var idToken = tokenData.TryGetProperty("id_token", out var idt)
                ? idt.GetString() : null;
            var accessToken = tokenData.TryGetProperty("access_token", out var at)
                ? at.GetString() : null;

            return (idToken, accessToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exchange Auth0 auth code");
            return (null, null);
        }
    }

    public async Task<Auth0UserInfo?> GetUserInfoAsync(string accessToken)
    {
        try
        {
            var domain = _options.Domain.StartsWith("http")
                ? _options.Domain
                : $"https://{_options.Domain}";

            var request = new HttpRequestMessage(HttpMethod.Get, $"{domain}/userinfo");
            request.Headers.Authorization = new("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Auth0 user info request failed: {StatusCode} - {Error}", response.StatusCode, error);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            return new Auth0UserInfo(
                UserId: data.GetProperty("sub").GetString()!,
                Email: data.GetProperty("email").GetString()!,
                Name: data.TryGetProperty("name", out var name)
                    ? name.GetString()!
                    : data.GetProperty("email").GetString()!.Split('@')[0],
                Picture: data.TryGetProperty("picture", out var pic)
                    ? pic.GetString() : null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Auth0 user info");
            return null;
        }
    }

    public async Task UpdateUserRolesAsync(string auth0UserId, Dictionary<string, List<string>> applicationRoles)
    {
        try
        {
            var token = await GetManagementApiTokenAsync();
            if (token == null)
            {
                _logger.LogError("Failed to get Management API token");
                return;
            }

            var domain = _options.Domain.StartsWith("http")
                ? new Uri(_options.Domain).Host
                : _options.Domain;

            using var managementClient = new ManagementApiClient(token, domain);

            var metadata = new
            {
                session_manager_roles = applicationRoles,
                session_manager_synced_at = DateTime.UtcNow.ToString("o")
            };

            await managementClient.Users.UpdateAsync(auth0UserId,
                new UserUpdateRequest { AppMetadata = metadata });

            _logger.LogInformation("Updated Auth0 user {UserId} with {Count} application roles",
                auth0UserId, applicationRoles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Auth0 user roles for {UserId}", auth0UserId);
        }
    }

    public async Task DeleteUserAsync(string auth0UserId)
    {
        try
        {
            var token = await GetManagementApiTokenAsync();
            if (token == null)
            {
                _logger.LogError("Failed to get Management API token for deleting user {UserId}", auth0UserId);
                return;
            }

            var domain = _options.Domain.StartsWith("http")
                ? new Uri(_options.Domain).Host
                : _options.Domain;

            using var managementClient = new ManagementApiClient(token, domain);
            await managementClient.Users.DeleteAsync(auth0UserId);

            _logger.LogInformation("Deleted user {UserId} from Auth0", auth0UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user {UserId} from Auth0", auth0UserId);
        }
    }

    private async Task<string?> GetManagementApiTokenAsync()
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ManagementApiClientId,
                ["client_secret"] = _options.ManagementApiClientSecret,
                ["audience"] = _options.ManagementApiIdentifier
            });

            var domain = _options.Domain.StartsWith("http")
                ? _options.Domain
                : $"https://{_options.Domain}";

            var response = await _httpClient.PostAsync($"{domain}/oauth/token", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Auth0 Management API token request failed: {Error}", error);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

            return tokenData.GetProperty("access_token").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Management API token");
            return null;
        }
    }
}
