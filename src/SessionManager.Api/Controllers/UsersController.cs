using Microsoft.AspNetCore.Mvc;
using SessionManager.Api.Models.Users;
using SessionManager.Api.Services;

namespace SessionManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
    {
        var users = await _userService.GetAllAsync();
        return Ok(users);
    }

    /// <summary>
    /// Get a specific user by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(Guid id)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        return Ok(user);
    }

    /// <summary>
    /// Assign roles to a user
    /// </summary>
    [HttpPut("{userId}/roles")]
    public async Task<ActionResult> AssignRoles(
        Guid userId,
        [FromBody] AssignRoleRequest request)
    {
        var result = await _userService.AssignRolesAsync(userId, request.RoleIds);
        if (!result)
        {
            return NotFound(new { error = "User not found" });
        }

        return Ok(new { message = "Roles assigned" });
    }

    /// <summary>
    /// Remove a role from a user
    /// </summary>
    [HttpDelete("{userId}/roles/{roleId}")]
    public async Task<ActionResult> RemoveRole(Guid userId, Guid roleId)
    {
        var result = await _userService.RemoveRoleAsync(userId, roleId);
        if (!result)
        {
            return NotFound(new { error = "User role not found" });
        }

        return Ok(new { message = "Role removed" });
    }

    /// <summary>
    /// Set user active status
    /// </summary>
    [HttpPut("{userId}/active")]
    public async Task<ActionResult> SetActive(Guid userId, [FromBody] bool isActive)
    {
        var result = await _userService.SetActiveAsync(userId, isActive);
        if (!result)
        {
            return NotFound(new { error = "User not found" });
        }

        return Ok(new { message = $"User {(isActive ? "enabled" : "disabled")}" });
    }

    /// <summary>
    /// Delete a user (cannot delete super admin)
    /// </summary>
    [HttpDelete("{userId}")]
    public async Task<ActionResult> Delete(Guid userId)
    {
        var result = await _userService.DeleteAsync(userId);
        if (!result)
        {
            return BadRequest(new { error = "Cannot delete user. User may not exist or is a super admin." });
        }

        return Ok(new { message = "User deleted successfully" });
    }
}
