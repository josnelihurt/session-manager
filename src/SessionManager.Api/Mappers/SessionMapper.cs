using SessionManager.Api.Models;

namespace SessionManager.Api.Mappers;

public static class SessionMapper
{
    public static SessionDto ToDto(SessionInfo session)
    {
        return new SessionDto(
            SessionId: session.SessionId,
            CookiePrefix: session.CookiePrefix,
            Ttl: session.TtlMilliseconds,
            ExpiresAt: session.ExpiresAt?.ToString("o"),
            Remaining: FormatRemainingTime(session.TtlMilliseconds),
            FullKey: session.FullKey,
            UserId: session.UserId?.ToString("N"),
            Username: session.Username,
            Email: session.Email
        );
    }

    public static List<SessionDto> ToDto(IEnumerable<SessionInfo> sessions)
        => sessions.Select(ToDto).ToList();

    private static string FormatRemainingTime(long? ttlMilliseconds)
    {
        if (!ttlMilliseconds.HasValue || ttlMilliseconds.Value <= 0)
            return "Expired";

        var span = TimeSpan.FromMilliseconds(ttlMilliseconds.Value);

        return span.TotalDays >= 1
            ? $"{(int)span.TotalDays}d {span.Hours}h"
            : span.TotalHours >= 1
                ? $"{(int)span.TotalHours}h {span.Minutes}m"
                : $"{span.Minutes}m {span.Seconds}s";
    }
}
