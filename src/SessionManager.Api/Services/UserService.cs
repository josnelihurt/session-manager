using Microsoft.EntityFrameworkCore;
using SessionManager.Api.Data;
using SessionManager.Api.Entities;
using SessionManager.Api.Models.Users;

namespace SessionManager.Api.Services;

public class UserService : IUserService
{
    private readonly SessionManagerDbContext _dbContext;
    private readonly ILogger<UserService> _logger;

    public UserService(
        SessionManagerDbContext dbContext,
        ILogger<UserService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        var users = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Application)
            .OrderBy(u => u.Username)
            .ToListAsync();

        return users.Select(MapToDto);
    }

    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Application)
            .FirstOrDefaultAsync(u => u.Id == id);

        return user != null ? MapToDto(user) : null;
    }

    public async Task<UserDto?> GetByEmailAsync(string email)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Application)
            .FirstOrDefaultAsync(u => u.Email == email);

        return user != null ? MapToDto(user) : null;
    }

    public async Task<bool> AssignRolesAsync(Guid userId, Guid[] roleIds)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return false;

        var existingRoleIds = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();

        foreach (var roleId in roleIds)
        {
            if (!existingRoleIds.Contains(roleId))
            {
                var role = await _dbContext.Roles.FindAsync(roleId);
                if (role != null)
                {
                    user.UserRoles.Add(new UserRole
                    {
                        UserId = userId,
                        RoleId = roleId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Assigned roles {RoleIds} to user {UserId}", roleIds, userId);

        return true;
    }

    public async Task<bool> RemoveRoleAsync(Guid userId, Guid roleId)
    {
        var userRole = await _dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (userRole == null) return false;

        _dbContext.UserRoles.Remove(userRole);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Removed role {RoleId} from user {UserId}", roleId, userId);

        return true;
    }

    public async Task<bool> SetActiveAsync(Guid userId, bool isActive)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) return false;

        user.IsActive = isActive;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} active status set to {IsActive}", userId, isActive);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for deletion", userId);
            return false;
        }

        // Prevent deletion of super admin
        if (user.IsSuperAdmin)
        {
            _logger.LogWarning("Attempted to delete super admin user {UserId}", userId);
            return false;
        }

        // Remove all user roles
        _dbContext.UserRoles.RemoveRange(user.UserRoles);

        // Remove the user
        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted user {UserId} ({Username})", userId, user.Username);

        return true;
    }

    private static UserDto MapToDto(User user)
    {
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
                .ToArray()
        );
    }
}
