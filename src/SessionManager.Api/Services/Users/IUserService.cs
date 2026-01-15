using SessionManager.Api.Models.Users;

namespace SessionManager.Api.Services.Users;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllAsync();
    Task<UserDto?> GetByIdAsync(Guid id);
    Task<UserDto?> GetByEmailAsync(string email);
    Task<bool> AssignRolesAsync(Guid userId, Guid[] roleIds);
    Task<bool> RemoveRoleAsync(Guid userId, Guid roleId);
    Task<bool> SetActiveAsync(Guid userId, bool isActive);
    Task<bool> SetCanImpersonateAsync(Guid userId, bool canImpersonate);
    Task<bool> DeleteAsync(Guid userId);
}
