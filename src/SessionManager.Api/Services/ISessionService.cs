using SessionManager.Api.Models;

namespace SessionManager.Api.Services;

public interface ISessionService
{
    Task<List<SessionInfo>> GetAllSessionsAsync();
    Task<bool> DeleteSessionAsync(string fullKey);
    Task<int> DeleteAllSessionsAsync();
}
