using SessionManager.Api.Models.Auth;

namespace SessionManager.Api.Services.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, string userAgent);
    Task<UserInfo?> ValidateCredentialsAsync(string username, string password);
    Task<LoginResponse> CreateSessionForUserAsync(Guid userId, string ipAddress, string userAgent);
    Task<LoginResponse> RegisterAsync(RegisterRequest request, string ipAddress, string userAgent);
    Task<bool> LogoutAsync(string sessionKey);
    Task<UserInfo?> GetCurrentUserAsync(string sessionKey);
    Task<IEnumerable<ProviderInfo>> GetProvidersAsync();
    Task<bool> CanAccessApplicationAsync(string sessionKey, string applicationUrl);
}
