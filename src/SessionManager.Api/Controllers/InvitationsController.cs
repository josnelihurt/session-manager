using Microsoft.AspNetCore.Mvc;
using SessionManager.Api.Models.Invitations;
using SessionManager.Api.Services;

namespace SessionManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;
    private readonly IAuthService _authService;
    private readonly ILogger<InvitationsController> _logger;

    public InvitationsController(
        IInvitationService invitationService,
        IAuthService authService,
        ILogger<InvitationsController> logger)
    {
        _invitationService = invitationService;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Get all invitations
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InvitationDto>>> GetAll()
    {
        var invitations = await _invitationService.GetAllAsync();
        return Ok(invitations);
    }

    /// <summary>
    /// Create a new invitation
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<InvitationDto>> Create([FromBody] CreateInvitationRequest request)
    {
        // Get current user from session
        var sessionKey = Request.Cookies["_session_manager"];
        if (string.IsNullOrEmpty(sessionKey))
        {
            return Unauthorized(new { error = "Not authenticated" });
        }

        var user = await _authService.GetCurrentUserAsync(sessionKey);
        if (user == null)
        {
            return Unauthorized(new { error = "Invalid session" });
        }

        try
        {
            var invitation = await _invitationService.CreateAsync(request, user.Id);
            if (invitation == null)
            {
                return BadRequest(new { error = "Failed to create invitation" });
            }
            return CreatedAtAction(nameof(GetAll), new { id = invitation.Id }, invitation);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid invitation creation attempt");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating invitation");
            return BadRequest(new { error = "Failed to create invitation" });
        }
    }

    /// <summary>
    /// Delete an invitation
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var result = await _invitationService.DeleteAsync(id);
        if (!result)
        {
            return NotFound(new { error = "Invitation not found" });
        }

        return Ok(new { message = "Invitation deleted" });
    }

    /// <summary>
    /// Get invitation by token (used for validation during registration)
    /// </summary>
    [HttpGet("validate/{token}")]
    public async Task<ActionResult<InvitationDto>> ValidateToken(string token)
    {
        var invitation = await _invitationService.GetByTokenAsync(token);
        if (invitation == null)
        {
            return NotFound(new { error = "Invalid invitation token" });
        }

        // Check if expired
        if (invitation.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new { error = "Invitation has expired" });
        }

        // Check if already used
        if (invitation.IsUsed)
        {
            return BadRequest(new { error = "Invitation has already been used" });
        }

        return Ok(invitation);
    }
}
