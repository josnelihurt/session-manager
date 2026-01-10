namespace SessionManager.Api.Models;

public record ApiResponse<T>(bool Success, T? Data = default, string? Message = null, string? Error = null);

public record SessionsResponse(bool Success, IEnumerable<SessionDto> Data, int Count);

public record SessionDto(
    string SessionId,
    string CookiePrefix,
    long Ttl,
    string? ExpiresAt,
    string Remaining,
    string FullKey
);

public record DeleteResponse(bool Success, string Message);

public record DeleteAllResponse(bool Success, string Message, int Count);
