using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SessionManager.Api.Data;
using SessionManager.Api.Entities;
using SessionManager.Api.Models.Applications;

namespace SessionManager.Api.Services;

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

        return apps.Select(MapToDto);
    }

    public async Task<IEnumerable<ApplicationDto>> GetUserApplicationsAsync(Guid userId)
    {
        // Get user's roles
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

        return apps.Select(MapToDto);
    }

    public async Task<ApplicationDto?> GetByIdAsync(Guid id)
    {
        var app = await _dbContext.Applications
            .Include(a => a.Roles)
            .FirstOrDefaultAsync(a => a.Id == id);

        return app != null ? MapToDto(app) : null;
    }

    public async Task<ApplicationDto?> GetByUrlAsync(string url)
    {
        var app = await _dbContext.Applications
            .Include(a => a.Roles)
            .FirstOrDefaultAsync(a => a.Url == url);

        return app != null ? MapToDto(app) : null;
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
        var defaultRoles = new[] { "admin", "user", "viewer" };
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

        return MapRoleToDto(role);
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
            "admin" => new Dictionary<string, bool>
            {
                ["read"] = true,
                ["write"] = true,
                ["delete"] = true,
                ["admin"] = true
            },
            "user" => new Dictionary<string, bool>
            {
                ["read"] = true,
                ["write"] = true,
                ["delete"] = false,
                ["admin"] = false
            },
            "viewer" => new Dictionary<string, bool>
            {
                ["read"] = true,
                ["write"] = false,
                ["delete"] = false,
                ["admin"] = false
            },
            _ => new Dictionary<string, bool>()
        };

        return JsonSerializer.Serialize(permissions);
    }

    private static ApplicationDto MapToDto(Application app)
    {
        return new ApplicationDto(
            Id: app.Id,
            Url: app.Url,
            Name: app.Name,
            Description: app.Description,
            IsActive: app.IsActive,
            Roles: app.Roles.Select(MapRoleToDto).ToArray()
        );
    }

    private static RoleDto MapRoleToDto(Role role)
    {
        var permissions = string.IsNullOrEmpty(role.PermissionsJson)
            ? new Dictionary<string, bool>()
            : JsonSerializer.Deserialize<Dictionary<string, bool>>(role.PermissionsJson)
              ?? new Dictionary<string, bool>();

        return new RoleDto(
            Id: role.Id,
            ApplicationId: role.ApplicationId,
            Name: role.Name,
            Permissions: permissions
        );
    }
}
