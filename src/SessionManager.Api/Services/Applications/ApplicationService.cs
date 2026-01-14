using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SessionManager.Api.Configuration;
using SessionManager.Api.Data;
using SessionManager.Api.Entities;
using SessionManager.Api.Mappers;
using SessionManager.Api.Models.Applications;

namespace SessionManager.Api.Services.Applications;

public class ApplicationService : IApplicationService
{
    private readonly SessionManagerDbContext _dbContext;
    private readonly ILogger<ApplicationService> _logger;

    public ApplicationService(
        SessionManagerDbContext dbContext,
        ILogger<ApplicationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<ApplicationDto>> GetAllAsync()
    {
        var apps = await _dbContext.Applications
            .Include(a => a.Roles)
            .OrderBy(a => a.Name)
            .ToListAsync();

        return ApplicationMapper.ToDto(apps);
    }

    public async Task<IEnumerable<ApplicationDto>> GetUserApplicationsAsync(Guid userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) return Enumerable.Empty<ApplicationDto>();

        // Super admin can see all active applications
        if (user.IsSuperAdmin)
        {
            var allApps = await _dbContext.Applications
                .Include(a => a.Roles)
                .Where(a => a.IsActive)
                .OrderBy(a => a.Name)
                .ToListAsync();

            return ApplicationMapper.ToDto(allApps);
        }

        // Regular users get only applications they have roles for
        var userRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
            .ThenInclude(r => r.Application)
            .Select(ur => ur.Role.ApplicationId)
            .Distinct()
            .ToListAsync();

        var apps = await _dbContext.Applications
            .Include(a => a.Roles)
            .Where(a => userRoles.Contains(a.Id) && a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync();

        return ApplicationMapper.ToDto(apps);
    }

    public async Task<ApplicationDto?> GetByIdAsync(Guid id)
    {
        var app = await _dbContext.Applications
            .Include(a => a.Roles)
            .FirstOrDefaultAsync(a => a.Id == id);

        return app != null ? ApplicationMapper.ToDto(app) : null;
    }

    public async Task<ApplicationDto?> GetByUrlAsync(string url)
    {
        var app = await _dbContext.Applications
            .Include(a => a.Roles)
            .FirstOrDefaultAsync(a => a.Url == url);

        return app != null ? ApplicationMapper.ToDto(app) : null;
    }

    public async Task<ApplicationDto> CreateAsync(CreateApplicationRequest request)
    {
        var app = new Application
        {
            Url = request.Url.ToLowerInvariant(),
            Name = request.Name,
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Applications.Add(app);
        await _dbContext.SaveChangesAsync();

        // Create default roles
        var defaultRoles = new[] { SessionManagerConstants.AdminRole, SessionManagerConstants.UserRole, SessionManagerConstants.ViewerRole };
        foreach (var roleName in defaultRoles)
        {
            var role = new Role
            {
                ApplicationId = app.Id,
                Name = roleName,
                PermissionsJson = GetDefaultPermissions(roleName),
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Roles.Add(role);
        }
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created application {Name} with URL {Url}", app.Name, app.Url);

        return await GetByIdAsync(app.Id) ?? throw new InvalidOperationException("Failed to create application");
    }

    public async Task<ApplicationDto?> UpdateAsync(Guid id, CreateApplicationRequest request)
    {
        var app = await _dbContext.Applications.FindAsync(id);
        if (app == null) return null;

        app.Url = request.Url.ToLowerInvariant();
        app.Name = request.Name;
        app.Description = request.Description;
        app.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var app = await _dbContext.Applications.FindAsync(id);
        if (app == null) return false;

        _dbContext.Applications.Remove(app);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<RoleDto> CreateRoleAsync(Guid applicationId, CreateRoleRequest request)
    {
        var role = new Role
        {
            ApplicationId = applicationId,
            Name = request.Name,
            PermissionsJson = request.Permissions != null
                ? JsonSerializer.Serialize(request.Permissions)
                : "{}",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync();

        return RoleMapper.ToDto(role);
    }

    public async Task<bool> DeleteRoleAsync(Guid roleId)
    {
        var role = await _dbContext.Roles.FindAsync(roleId);
        if (role == null) return false;

        _dbContext.Roles.Remove(role);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task SeedFromConfigAsync(string[] allowedApplications)
    {
        foreach (var url in allowedApplications)
        {
            var normalizedUrl = url.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedUrl)) continue;

            var exists = await _dbContext.Applications
                .AnyAsync(a => a.Url == normalizedUrl);

            if (!exists)
            {
                var name = normalizedUrl.Split('.')[0];
                name = char.ToUpper(name[0]) + name[1..];

                await CreateAsync(new CreateApplicationRequest(
                    Url: normalizedUrl,
                    Name: name,
                    Description: null
                ));
            }
        }
    }

    private static string GetDefaultPermissions(string roleName)
    {
        var permissions = roleName switch
        {
            SessionManagerConstants.AdminRole => new Dictionary<string, bool>
            {
                [SessionManagerConstants.PermissionRead] = true,
                [SessionManagerConstants.PermissionWrite] = true,
                [SessionManagerConstants.PermissionDelete] = true,
                [SessionManagerConstants.PermissionAdmin] = true
            },
            SessionManagerConstants.UserRole => new Dictionary<string, bool>
            {
                [SessionManagerConstants.PermissionRead] = true,
                [SessionManagerConstants.PermissionWrite] = true,
                [SessionManagerConstants.PermissionDelete] = false,
                [SessionManagerConstants.PermissionAdmin] = false
            },
            SessionManagerConstants.ViewerRole => new Dictionary<string, bool>
            {
                [SessionManagerConstants.PermissionRead] = true,
                [SessionManagerConstants.PermissionWrite] = false,
                [SessionManagerConstants.PermissionDelete] = false,
                [SessionManagerConstants.PermissionAdmin] = false
            },
            _ => new Dictionary<string, bool>()
        };

        return JsonSerializer.Serialize(permissions);
    }
}
