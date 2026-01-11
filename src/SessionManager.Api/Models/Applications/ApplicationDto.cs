using System.Text.Json.Serialization;

namespace SessionManager.Api.Models.Applications;

public record ApplicationDto(
    Guid Id,
    string Url,
    string Name,
    string? Description,
    bool IsActive,
    RoleDto[] Roles
);

[JsonSerializable(typeof(ApplicationDto))]
[JsonSerializable(typeof(ApplicationDto[]))]
internal partial class AppJsonContextApplications : JsonSerializerContext { }
