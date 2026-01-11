using SessionManager.Api.Models.Auth;

namespace SessionManager.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, string userAgent);
    Task<LoginResponse> RegisterAsync(RegisterRequest request, string ipAddress, string userAgent);
    Task<bool> LogoutAsync(string sessionKey);
    Task<UserInfo?> GetCurrentUserAsync(string sessionKey);
    Task<IEnumerable<ProviderInfo>> GetProvidersAsync();
    Task<bool> CanAccessApplicationAsync(string sessionKey, string applicationUrl);
}
