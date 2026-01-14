using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Models.Invitations;
using SessionManager.Api.Services.Invitations;
using SessionManager.Api.Services.Auth;
using SessionManager.Api.Services.Applications;
using SessionManager.Api.Services.Email;

namespace SessionManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;
    private readonly IAuthService _authService;
    private readonly IApplicationService _applicationService;
    private readonly IEmailService _emailService;
    private readonly ILogger<InvitationsController> _logger;
    private readonly AuthOptions _authOptions;

    public InvitationsController(
        IInvitationService invitationService,
        IAuthService authService,
        IApplicationService applicationService,
        IEmailService emailService,
        ILogger<InvitationsController> logger,
        IOptions<AuthOptions> authOptions)
    {
        _invitationService = invitationService;
        _authService = authService;
        _applicationService = applicationService;
        _emailService = emailService;
        _logger = logger;
        _authOptions = authOptions.Value;
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
        var sessionKey = Request.Cookies[_authOptions.CookieName];
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

            // Queue invitation email if requested
            if (request.SendEmail)
            {
                try
                {
                    // Get application URLs for the roles
                    var applicationUrls = await GetApplicationUrlsForRoles(request.PreAssignedRoleIds);

                    await _emailService.EnqueueInvitationEmailAsync(invitation, applicationUrls);
                    _logger.LogInformation("Invitation email queued for {Email}", invitation.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to queue invitation email to {Email}", invitation.Email);
                    // Don't fail the request if email queueing fails
                }
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

    private async Task<string[]> GetApplicationUrlsForRoles(Guid[]? roleIds)
    {
        if (roleIds == null || roleIds.Length == 0)
        {
            return Array.Empty<string>();
        }

        try
        {
            var allApplications = await _applicationService.GetAllAsync();
            var applicationsWithRoles = allApplications
                .Where(app => app.Roles != null && app.Roles.Any(role => roleIds.Contains(role.Id)))
                .Select(app => app.Url)
                .Distinct()
                .ToArray();

            return applicationsWithRoles;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get application URLs for roles");
            return Array.Empty<string>();
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

    /// <summary>
    /// Resend invitation email
    /// </summary>
    [HttpPost("{id}/resend-email")]
    public async Task<ActionResult> ResendEmail(Guid id)
    {
        var sessionKey = Request.Cookies[_authOptions.CookieName];
        if (string.IsNullOrEmpty(sessionKey))
        {
            return Unauthorized(new { error = "Not authenticated" });
        }

        var user = await _authService.GetCurrentUserAsync(sessionKey);
        if (user == null)
        {
            return Unauthorized(new { error = "Invalid session" });
        }

        var invitation = await _invitationService.GetByIdAsync(id);
        if (invitation == null)
        {
            return NotFound(new { error = "Invitation not found" });
        }

        try
        {
            // Convert string roles to Guid array for getting application URLs
            var roleIds = invitation.PreAssignedRoles
                .Select(r => Guid.TryParse(r, out var guid) ? guid : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g.Value)
                .ToArray();

            var applicationUrls = await GetApplicationUrlsForRoles(roleIds);

            await _emailService.EnqueueInvitationEmailAsync(invitation, applicationUrls);
            _logger.LogInformation("Resend email queued for {Email}", invitation.Email);
            return Ok(new { message = "Email queued successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending invitation email to {Email}", invitation.Email);
            return BadRequest(new { error = "Failed to send email" });
        }
    }
}
