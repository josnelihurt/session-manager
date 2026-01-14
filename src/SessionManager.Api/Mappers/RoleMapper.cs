using System.Text.Json;
using SessionManager.Api.Entities;
using SessionManager.Api.Models.Applications;

namespace SessionManager.Api.Mappers;

public static class RoleMapper
{
    public static RoleDto ToDto(Role role)
    {
        Dictionary<string, bool> permissions;

        if (string.IsNullOrEmpty(role.PermissionsJson))
        {
            permissions = new Dictionary<string, bool>();
        }
        else
        {
            try
            {
                permissions = JsonSerializer.Deserialize<Dictionary<string, bool>>(role.PermissionsJson)
                    ?? new Dictionary<string, bool>();
            }
            catch (JsonException)
            {
                permissions = new Dictionary<string, bool>();
            }
        }

        return new RoleDto(
            Id: role.Id,
            ApplicationId: role.ApplicationId,
            Name: role.Name,
            Permissions: permissions
        );
    }

    public static List<RoleDto> ToDto(IEnumerable<Role> roles)
        => roles.Select(ToDto).ToList();
}
