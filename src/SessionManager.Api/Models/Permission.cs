using SessionManager.Api.Configuration;

namespace SessionManager.Api.Models;

[Flags]
public enum Permission
{
    None = 0,
    Read = 1,
    Write = 2,
    Delete = 4,
    Admin = 8,

    // Combinations
    ReadWrite = Read | Write,
    Full = Read | Write | Delete | Admin
}

public static class PermissionExtensions
{
    public static Permission FromDictionary(Dictionary<string, bool>? dict)
    {
        if (dict == null) return Permission.None;

        var permissions = Permission.None;
        if (dict.GetValueOrDefault(SessionManagerConstants.PermissionRead)) permissions |= Permission.Read;
        if (dict.GetValueOrDefault(SessionManagerConstants.PermissionWrite)) permissions |= Permission.Write;
        if (dict.GetValueOrDefault(SessionManagerConstants.PermissionDelete)) permissions |= Permission.Delete;
        if (dict.GetValueOrDefault(SessionManagerConstants.PermissionAdmin)) permissions |= Permission.Admin;

        return permissions;
    }

    public static Dictionary<string, bool> ToDictionary(this Permission permission)
    {
        return new Dictionary<string, bool>
        {
            [SessionManagerConstants.PermissionRead] = permission.HasFlag(Permission.Read),
            [SessionManagerConstants.PermissionWrite] = permission.HasFlag(Permission.Write),
            [SessionManagerConstants.PermissionDelete] = permission.HasFlag(Permission.Delete),
            [SessionManagerConstants.PermissionAdmin] = permission.HasFlag(Permission.Admin)
        };
    }
}
