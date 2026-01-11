using System.Text.Json.Serialization;

namespace SessionManager.Api.Models.Applications;

public record RoleDto(
    Guid Id,
    Guid ApplicationId,
    string Name,
    Dictionary<string, bool> Permissions
);

[JsonSerializable(typeof(RoleDto))]
[JsonSerializable(typeof(RoleDto[]))]
internal partial class AppJsonContextRoles : JsonSerializerContext { }
