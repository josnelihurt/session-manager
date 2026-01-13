using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Models.Invitations;
using SessionManager.Api.Services;

namespace SessionManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;
    private readonly IAuthService _authService;
    private readonly IEmailService _emailService;
    private readonly IApplicationService _applicationService;
    private readonly ILogger<InvitationsController> _logger;
    private readonly AuthOptions _authOptions;

    public InvitationsController(
        IInvitationService invitationService,
        IAuthService authService,
        IEmailService emailService,
        IApplicationService applicationService,
        ILogger<InvitationsController> logger,
        IOptions<AuthOptions> authOptions)
    {
        _invitationService = invitationService;
        _authService = authService;
        _emailService = emailService;
        _applicationService = applicationService;
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

            // Send invitation email if requested
            if (request.SendEmail)
            {
                try
                {
                    // Get application URLs for the roles
                    var applicationUrls = await GetApplicationUrlsForRoles(request.PreAssignedRoleIds);

                    var emailSent = await _emailService.SendInvitationEmailAsync(
                        invitation.Email,
                        invitation,
                        applicationUrls
                    );

                    if (!emailSent)
                    {
                        _logger.LogWarning("Invitation created but email failed to send to {Email}", invitation.Email);
                    }
                    else
                    {
                        _logger.LogInformation("Invitation email sent to {Email}", invitation.Email);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send invitation email to {Email}", invitation.Email);
                    // Don't fail the request if email sending fails
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
}
