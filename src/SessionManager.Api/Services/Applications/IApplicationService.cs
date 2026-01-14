using SessionManager.Api.Models.Applications;

namespace SessionManager.Api.Services.Applications;

public interface IApplicationService
{
    Task<IEnumerable<ApplicationDto>> GetAllAsync();
    Task<IEnumerable<ApplicationDto>> GetUserApplicationsAsync(Guid userId);
    Task<ApplicationDto?> GetByIdAsync(Guid id);
    Task<ApplicationDto?> GetByUrlAsync(string url);
    Task<ApplicationDto> CreateAsync(CreateApplicationRequest request);
    Task<ApplicationDto?> UpdateAsync(Guid id, CreateApplicationRequest request);
    Task<bool> DeleteAsync(Guid id);
    Task<RoleDto> CreateRoleAsync(Guid applicationId, CreateRoleRequest request);
    Task<bool> DeleteRoleAsync(Guid roleId);
    Task SeedFromConfigAsync(string[] allowedApplications);
}
