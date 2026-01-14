using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Models;
using SessionManager.Api.Models.Auth;
using StackExchange.Redis;
using System.Text.Json;

namespace SessionManager.Api.Services.Sessions;

public class RedisSessionService : ISessionService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisSessionService> _logger;

    public RedisSessionService(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options,
        ILogger<RedisSessionService> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<SessionInfo>> GetAllSessionsAsync()
    {
        var db = _redis.GetDatabase();
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var sessions = new List<SessionInfo>();

        var sessionKeys = server.Keys(pattern: _options.SessionKeyPattern, pageSize: _options.ScanPageSize).ToList();

        _logger.LogInformation("Found {Count} session keys", sessionKeys.Count);

        foreach (var key in sessionKeys)
        {
            var keyStr = key.ToString();
            var ttl = await db.KeyTimeToLiveAsync(key);
            var ttlMs = ttl.HasValue ? (long)ttl.Value.TotalMilliseconds : -1;

            var (sessionId, cookiePrefix) = ParseSessionKey(keyStr);
            var expiresAt = ttlMs > 0 ? DateTime.UtcNow.AddMilliseconds(ttlMs) : (DateTime?)null;

            // Try to get user data from session value
            Guid? userId = null;
            string? username = null;
            string? email = null;

            try
            {
                var json = await db.StringGetAsync(key);
                if (!json.IsNullOrEmpty)
                {
                    var sessionData = JsonSerializer.Deserialize(json!, AppJsonContext.Default.SessionData);
                    if (sessionData != null)
                    {
                        userId = sessionData.UserId;
                        username = sessionData.Username;
                        email = sessionData.Email;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize session data for key {Key}", keyStr);
            }

            sessions.Add(new SessionInfo(
                sessionId,
                cookiePrefix,
                ttlMs,
                expiresAt,
                keyStr,
                userId,
                username,
                email
            ));
        }

        return sessions.OrderBy(s => s.CookiePrefix).ThenBy(s => s.SessionId).ToList();
    }

    public async Task<bool> DeleteSessionAsync(string fullKey)
    {
        var db = _redis.GetDatabase();
        var result = await db.KeyDeleteAsync(fullKey);
        _logger.LogInformation("Deleted session {Key}: {Result}", fullKey, result);
        return result;
    }

    public async Task<int> DeleteAllSessionsAsync()
    {
        var db = _redis.GetDatabase();
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var count = 0;

        var keys = server.Keys(pattern: _options.SessionKeyPattern, pageSize: _options.ScanPageSize).ToList();

        foreach (var key in keys)
        {
            if (await db.KeyDeleteAsync(key))
            {
                count++;
            }
        }

        _logger.LogInformation("Deleted {Count} sessions", count);
        return count;
    }

    public async Task<string> CreateSessionAsync(Guid userId, string username, string email, bool isSuperAdmin, bool canImpersonate, string ipAddress, string userAgent)
    {
        var sessionKey = $"{SessionManagerConstants.RedisSessionPrefix}{Guid.NewGuid():N}";
        var db = _redis.GetDatabase();

        // Create session data with 4 hour expiry
        var sessionData = new SessionData(
            UserId: userId,
            Username: username,
            Email: email,
            IsSuperAdmin: isSuperAdmin,
            ExpiresAt: DateTime.UtcNow.AddHours(4),
            CanImpersonate: canImpersonate
        );

        var json = JsonSerializer.Serialize(sessionData, AppJsonContext.Default.SessionData);
        var expiry = TimeSpan.FromHours(4);

        await db.StringSetAsync(sessionKey, json, expiry);

        _logger.LogInformation("Created session {Session} for user {User}", sessionKey, username);

        return sessionKey;
    }

    public async Task<string> CreateImpersonatedSessionAsync(
        Guid userId,
        string username,
        string email,
        bool isSuperAdmin,
        ImpersonatorInfo impersonator,
        Guid impersonationId,
        DateTime expiresAt,
        string ipAddress,
        string userAgent)
    {
        var sessionKey = $"{SessionManagerConstants.RedisSessionPrefix}{Guid.NewGuid():N}";
        var db = _redis.GetDatabase();

        // Create impersonated session data (impersonated users cannot impersonate others)
        var sessionData = new SessionData(
            UserId: userId,
            Username: username,
            Email: email,
            IsSuperAdmin: isSuperAdmin,
            ExpiresAt: expiresAt,
            CanImpersonate: false,
            IsImpersonated: true,
            Impersonator: impersonator,
            ImpersonationId: impersonationId,
            ImpersonationExpiresAt: expiresAt
        );

        var json = JsonSerializer.Serialize(sessionData, AppJsonContext.Default.SessionData);
        var expiry = expiresAt - DateTime.UtcNow;

        await db.StringSetAsync(sessionKey, json, expiry);

        _logger.LogWarning("Created impersonated session {Session} for user {User} by admin {Admin}",
            sessionKey, username, impersonator.Username);

        return sessionKey;
    }

    public async Task<SessionData?> GetSessionAsync(string sessionKey)
    {
        var db = _redis.GetDatabase();
        var json = await db.StringGetAsync(sessionKey);

        if (json.IsNullOrEmpty) return null;

        return JsonSerializer.Deserialize(json!, AppJsonContext.Default.SessionData);
    }

    public async Task<bool> InvalidateSessionAsync(string sessionKey)
    {
        var db = _redis.GetDatabase();
        var deleted = await db.KeyDeleteAsync(sessionKey);

        if (deleted)
        {
            _logger.LogInformation("Invalidated session {Session}", sessionKey);
        }

        return deleted;
    }

    public async Task<List<SessionInfo>> GetSessionsByUserAsync(Guid userId)
    {
        var allSessions = await GetAllSessionsAsync();
        return allSessions.Where(s => s.UserId == userId).ToList();
    }

    public async Task<int> DeleteUserSessionsAsync(Guid userId)
    {
        var db = _redis.GetDatabase();
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var count = 0;

        var sessionKeys = server.Keys(pattern: _options.SessionKeyPattern, pageSize: _options.ScanPageSize).ToList();

        foreach (var key in sessionKeys)
        {
            try
            {
                var json = await db.StringGetAsync(key);
                if (!json.IsNullOrEmpty)
                {
                    var sessionData = JsonSerializer.Deserialize(json!, AppJsonContext.Default.SessionData);
                    if (sessionData != null && sessionData.UserId == userId)
                    {
                        if (await db.KeyDeleteAsync(key))
                        {
                            count++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check/delete session {Key}", key);
            }
        }

        _logger.LogInformation("Deleted {Count} sessions for user {UserId}", count, userId);
        return count;
    }

    private static (string SessionId, string CookiePrefix) ParseSessionKey(string key)
    {
        if (key.Contains('-'))
        {
            var parts = key.Split('-');
            var cookiePrefix = string.Join('-', parts.Take(parts.Length - 1));
            var sessionId = parts.Last();
            return (sessionId, cookiePrefix);
        }

        return (key, "unknown");
    }
}
