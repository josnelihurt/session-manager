using SessionManager.Api.Entities;
using SessionManager.Api.Models.Users;

namespace SessionManager.Api.Mappers;

public static class UserMapper
{
    public static UserDto ToDto(User user)
    {
        // Super admins always have impersonate permission
        // For other users, use the CanImpersonate field
        bool canImpersonate = user.IsSuperAdmin || user.CanImpersonate;

        return new UserDto(
            Id: user.Id,
            Username: user.Username,
            Email: user.Email,
            Provider: user.Provider,
            IsSuperAdmin: user.IsSuperAdmin,
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt,
            Roles: user.UserRoles
                .Select(ur => new UserRoleDto(
                    RoleId: ur.RoleId,
                    RoleName: ur.Role.Name,
                    ApplicationName: ur.Role.Application.Name,
                    ApplicationUrl: ur.Role.Application.Url
                ))
                .ToArray(),
            CanImpersonate: canImpersonate
        );
    }

    public static List<UserDto> ToDto(IEnumerable<User> users)
        => users.Select(ToDto).ToList();
}
