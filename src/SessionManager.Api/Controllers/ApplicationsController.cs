using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Models.Applications;
using SessionManager.Api.Services.Applications;
using SessionManager.Api.Services.Auth;
using System.Text.Json;

namespace SessionManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _applicationService;
    private readonly ILogger<ApplicationsController> _logger;
    private readonly AuthOptions _authOptions;

    public ApplicationsController(
        IApplicationService applicationService,
        ILogger<ApplicationsController> logger,
        IOptions<AuthOptions> authOptions)
    {
        _applicationService = applicationService;
        _logger = logger;
        _authOptions = authOptions.Value;
    }

    /// <summary>
    /// Get all applications (admin only)
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<ApplicationDto>>> GetAll()
    {
        var apps = await _applicationService.GetAllAsync();
        return Ok(apps);
    }

    /// <summary>
    /// Get applications accessible by current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ApplicationDto>>> GetMyApps(
        [FromServices] IAuthService authService)
    {
        var sessionKey = Request.Cookies[_authOptions.CookieName];
        if (string.IsNullOrEmpty(sessionKey))
        {
            return Unauthorized(new { error = "Not authenticated" });
        }

        var user = await authService.GetCurrentUserAsync(sessionKey);
        if (user == null)
        {
            return Unauthorized(new { error = "Invalid session" });
        }

        var apps = await _applicationService.GetUserApplicationsAsync(user.Id);
        return Ok(apps);
    }

    /// <summary>
    /// Create a new application
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApplicationDto>> Create([FromBody] CreateApplicationRequest request)
    {
        try
        {
            var app = await _applicationService.CreateAsync(request);
            return CreatedAtAction(nameof(GetAll), new { id = app.Id }, app);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating application");
            return BadRequest(new { error = "Failed to create application" });
        }
    }

    /// <summary>
    /// Update an application
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApplicationDto>> Update(
        Guid id,
        [FromBody] CreateApplicationRequest request)
    {
        var app = await _applicationService.UpdateAsync(id, request);
        if (app == null)
        {
            return NotFound(new { error = "Application not found" });
        }

        return Ok(app);
    }

    /// <summary>
    /// Delete an application
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var result = await _applicationService.DeleteAsync(id);
        if (!result)
        {
            return NotFound(new { error = "Application not found" });
        }

        return Ok(new { message = "Application deleted" });
    }

    /// <summary>
    /// Create a role for an application
    /// </summary>
    [HttpPost("{applicationId}/roles")]
    public async Task<ActionResult<RoleDto>> CreateRole(
        Guid applicationId,
        [FromBody] CreateRoleRequest request)
    {
        try
        {
            var role = await _applicationService.CreateRoleAsync(applicationId, request);
            return Ok(role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role");
            return BadRequest(new { error = "Failed to create role" });
        }
    }

    /// <summary>
    /// Delete a role
    /// </summary>
    [HttpDelete("roles/{roleId}")]
    public async Task<ActionResult> DeleteRole(Guid roleId)
    {
        var result = await _applicationService.DeleteRoleAsync(roleId);
        if (!result)
        {
            return NotFound(new { error = "Role not found" });
        }

        return Ok(new { message = "Role deleted" });
    }
}
