namespace SessionManager.Api.Models;

public record SessionInfo(
    string SessionId,
    string CookiePrefix,
    long TtlMilliseconds,
    DateTime? ExpiresAt,
    string FullKey
);
