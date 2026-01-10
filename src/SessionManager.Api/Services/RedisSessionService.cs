using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Models;
using StackExchange.Redis;

namespace SessionManager.Api.Services;

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

            sessions.Add(new SessionInfo(
                sessionId,
                cookiePrefix,
                ttlMs,
                expiresAt,
                keyStr
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
