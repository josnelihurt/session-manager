using SessionManager.Api.Entities;
using SessionManager.Api.Models.Applications;

namespace SessionManager.Api.Mappers;

public static class ApplicationMapper
{
    public static ApplicationDto ToDto(Application app)
    {
        return new ApplicationDto(
            Id: app.Id,
            Url: app.Url,
            Name: app.Name,
            Description: app.Description,
            IsActive: app.IsActive,
            Roles: app.Roles.Select(RoleMapper.ToDto).ToArray()
        );
    }

    public static List<ApplicationDto> ToDto(IEnumerable<Application> applications)
        => applications.Select(ToDto).ToList();
}
