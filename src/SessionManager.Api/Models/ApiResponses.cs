using System.Text.Json.Serialization;
using SessionManager.Api.Models.Auth;
using SessionManager.Api.Models.Impersonation;

namespace SessionManager.Api.Models;

public record ApiResponse<T>(bool Success, T? Data = default, string? Message = null, string? Error = null);

[JsonSerializable(typeof(SessionsResponse))]
[JsonSerializable(typeof(DeleteResponse))]
[JsonSerializable(typeof(DeleteAllResponse))]
[JsonSerializable(typeof(SessionDto))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(LoginSuccessResponse))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(MessageResponse))]
[JsonSerializable(typeof(UserInfo))]
[JsonSerializable(typeof(ProviderInfo))]
[JsonSerializable(typeof(ProviderInfo[]))]
[JsonSerializable(typeof(IEnumerable<ProviderInfo>))]
[JsonSerializable(typeof(SessionData))]
[JsonSerializable(typeof(ImpersonationStatusResponse))]
[JsonSerializable(typeof(ImpersonationSessionDto))]
[JsonSerializable(typeof(IEnumerable<ImpersonationSessionDto>))]
internal partial class AppJsonContext : JsonSerializerContext { }

public record SessionsResponse(bool Success, IEnumerable<SessionDto> Data, int Count);

public record SessionDto(
    string SessionId,
    string CookiePrefix,
    long Ttl,
    string? ExpiresAt,
    string Remaining,
    string FullKey,
    string? UserId,
    string? Username,
    string? Email
);

public record DeleteResponse(bool Success, string Message);

public record DeleteAllResponse(bool Success, string Message, int Count);

public record MessageResponse(string Message);
