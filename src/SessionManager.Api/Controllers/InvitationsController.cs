using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Models.Emails;
using SessionManager.Api.Models.Invitations;
using SessionManager.Api.Services;
using StackExchange.Redis;

namespace SessionManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;
    private readonly IAuthService _authService;
    private readonly IApplicationService _applicationService;
    private readonly ILogger<InvitationsController> _logger;
    private readonly AuthOptions _authOptions;
    private readonly IConnectionMultiplexer _redis;
    private const string EmailQueueKey = "email:queue";

    public InvitationsController(
        IInvitationService invitationService,
        IAuthService authService,
        IApplicationService applicationService,
        ILogger<InvitationsController> logger,
        IOptions<AuthOptions> authOptions,
        IConnectionMultiplexer redis)
    {
        _invitationService = invitationService;
        _authService = authService;
        _applicationService = applicationService;
        _logger = logger;
        _authOptions = authOptions.Value;
        _redis = redis;
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

                    await EnqueueInvitationEmailAsync(invitation, applicationUrls);
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

            await EnqueueInvitationEmailAsync(invitation, applicationUrls);
            _logger.LogInformation("Resend email queued for {Email}", invitation.Email);
            return Ok(new { message = "Email queued successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending invitation email to {Email}", invitation.Email);
            return BadRequest(new { error = "Failed to send email" });
        }
    }

    private async Task EnqueueInvitationEmailAsync(InvitationDto invitation, string[] applicationUrls)
    {
        var subject = "You're invited to join josnelihurt.me Lab";
        var body = GenerateInvitationEmailBody(invitation, applicationUrls);

        var emailJob = new EmailJob(
            invitation.Email,
            subject,
            body,
            DateTime.UtcNow
        );

        var json = System.Text.Json.JsonSerializer.Serialize(emailJob);
        var db = _redis.GetDatabase();
        await db.ListLeftPushAsync(EmailQueueKey, json);
    }

    private string GenerateInvitationEmailBody(InvitationDto invitation, string[] applicationUrls)
    {
        var providerText = invitation.Provider switch
        {
            "google" => "Google",
            "local" => "Email/Password",
            _ => invitation.Provider
        };

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>Invitation to josnelihurt.me Lab</title>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body style=\"margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #0a0e27; color: #f2f2f2;\">");
        sb.AppendLine("    <div style=\"max-width: 600px; margin: 0 auto; background: linear-gradient(135deg, #0a0e27 0%, #1a1a3e 100%);\">");
        sb.AppendLine("        <div style=\"padding: 40px 20px; text-align: center; border-bottom: 2px solid rgba(244, 211, 94, 0.3);\">");
        sb.AppendLine("            <h1 style=\"margin: 0; color: #F4D35E; font-size: 28px; font-weight: 600;\">You're Invited!</h1>");
        sb.AppendLine("            <p style=\"margin: 10px 0 0; color: rgba(242, 242, 242, 0.8); font-size: 16px;\">Join the josnelihurt.me Lab</p>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div style=\"padding: 40px 30px;\">");
        sb.AppendLine("            <p style=\"margin: 0 0 20px; color: #f2f2f2; font-size: 16px; line-height: 1.6;\">Hello!</p>");
        sb.AppendLine("            <p style=\"margin: 0 0 30px; color: rgba(242, 242, 242, 0.9); font-size: 16px; line-height: 1.6;\">");
        sb.AppendLine($"                You have been invited to access the <strong style=\"color: #F4D35E;\">josnelihurt.me Lab</strong> ");
        sb.AppendLine($"                using <strong style=\"color: #F4D35E;\">{providerText}</strong> authentication.");
        sb.AppendLine("            </p>");

        if (applicationUrls != null && applicationUrls.Length > 0)
        {
            sb.AppendLine("            <div style=\"margin: 30px 0; padding: 25px; background: rgba(244, 211, 94, 0.1); border: 1px solid rgba(244, 211, 94, 0.3); border-radius: 8px;\">");
            sb.AppendLine("                <h2 style=\"margin: 0 0 15px; color: #F4D35E; font-size: 18px;\">Your Applications</h2>");
            sb.AppendLine("                <p style=\"margin: 0 0 20px; color: rgba(242, 242, 242, 0.8); font-size: 14px;\">You have been granted access to the following applications:</p>");
            sb.AppendLine("                <ul style=\"margin: 0; padding-left: 20px; color: #f2f2f2; font-size: 14px; line-height: 1.8;\">");

            foreach (var appUrl in applicationUrls)
            {
                sb.AppendLine($"                    <li style=\"margin-bottom: 8px;\"><a href=\"https://{appUrl}\" style=\"color: #F4D35E; text-decoration: none;\">{appUrl}</a></li>");
            }

            sb.AppendLine("                </ul>");
            sb.AppendLine("            </div>");
        }

        sb.AppendLine("            <div style=\"text-align: center; margin: 40px 0;\">");
        sb.AppendLine($"                <a href=\"{invitation.InviteUrl}\" style=\"display: inline-block; padding: 15px 40px; background: linear-gradient(135deg, #F4D35E 0%, #F2A900 100%); color: #1a1a1a; text-decoration: none; border-radius: 8px; font-weight: 600; font-size: 16px;\">");
        sb.AppendLine("                    Create Your Account");
        sb.AppendLine("                </a>");
        sb.AppendLine("            </div>");

        sb.AppendLine("            <div style=\"margin: 30px 0; padding: 20px; background: rgba(255, 255, 255, 0.05); border-radius: 8px; border: 1px solid rgba(255, 255, 255, 0.1);\">");
        sb.AppendLine("                <p style=\"margin: 0 0 10px; color: rgba(242, 242, 242, 0.7); font-size: 13px;\">");
        sb.AppendLine($"                    <strong>Invitation Type:</strong> {providerText}");
        sb.AppendLine("                </p>");
        sb.AppendLine("                <p style=\"margin: 5px 0 0; color: rgba(242, 242, 242, 0.7); font-size: 13px;\">");
        sb.AppendLine($"                    <strong>Expires:</strong> {invitation.ExpiresAt:MMM dd, yyyy}");
        sb.AppendLine("                </p>");
        sb.AppendLine("            </div>");

        sb.AppendLine("            <div style=\"margin: 40px 0; padding: 25px; text-align: center; border-top: 1px solid rgba(244, 211, 94, 0.2); border-bottom: 1px solid rgba(244, 211, 94, 0.2);\">");
        sb.AppendLine("                <p style=\"margin: 0; color: #F4D35E; font-size: 18px; font-style: italic; font-weight: 500;\">");
        sb.AppendLine("                    \"With great power comes great responsibility.\"");
        sb.AppendLine("                </p>");
        sb.AppendLine("                <p style=\"margin: 10px 0 0; color: rgba(242, 242, 242, 0.6); font-size: 12px;\">");
        sb.AppendLine("                    - Uncle Ben, Spider-Man");
        sb.AppendLine("                </p>");
        sb.AppendLine("            </div>");

        sb.AppendLine("            <p style=\"margin: 30px 0 0; text-align: center; color: rgba(242, 242, 242, 0.7); font-size: 14px;\">");
        sb.AppendLine("                After creating your account, you can access the dashboard at:<br>");
        sb.AppendLine("                <a href=\"https://session-manager.lab.josnelihurt.me/dashboard\" style=\"color: #F4D35E; text-decoration: none; font-weight: 500;\">session-manager.lab.josnelihurt.me/dashboard</a>");
        sb.AppendLine("            </p>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div style=\"padding: 30px; text-align: center; border-top: 1px solid rgba(255, 255, 255, 0.1); background: rgba(10, 14, 39, 0.5);\">");
        sb.AppendLine("            <p style=\"margin: 0; color: rgba(242, 242, 242, 0.5); font-size: 12px;\">");
        sb.AppendLine("                This is an automated message from Session Manager.");
        sb.AppendLine("            </p>");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
