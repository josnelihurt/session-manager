using System.Text.Json.Serialization;

namespace SessionManager.Api.Models.Users;

public record UserDto(
    Guid Id,
    string Username,
    string Email,
    string Provider,
    bool IsSuperAdmin,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    UserRoleDto[] Roles,
    bool CanImpersonate
);

public record UserRoleDto(
    Guid RoleId,
    string RoleName,
    string ApplicationName,
    string ApplicationUrl
);

public record AssignRoleRequest(Guid[] RoleIds);

[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(UserDto[]))]
[JsonSerializable(typeof(UserRoleDto))]
[JsonSerializable(typeof(UserRoleDto[]))]
[JsonSerializable(typeof(AssignRoleRequest))]
internal partial class AppJsonContextUsers : JsonSerializerContext { }
