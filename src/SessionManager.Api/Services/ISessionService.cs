using SessionManager.Api.Models;
using SessionManager.Api.Models.Auth;

namespace SessionManager.Api.Services;

public interface ISessionService
{
    // Existing oauth2-proxy methods
    Task<List<SessionInfo>> GetAllSessionsAsync();
    Task<bool> DeleteSessionAsync(string fullKey);
    Task<int> DeleteAllSessionsAsync();

    // New session-manager methods
    Task<string> CreateSessionAsync(Guid userId, string username, string email, bool isSuperAdmin, string ipAddress, string userAgent);
    Task<SessionData?> GetSessionAsync(string sessionKey);
    Task<bool> InvalidateSessionAsync(string sessionKey);
}
