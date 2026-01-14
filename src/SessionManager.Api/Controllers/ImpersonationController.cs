using Microsoft.AspNetCore.Mvc;
using SessionManager.Api.Configuration;
using SessionManager.Api.Models;
using SessionManager.Api.Models.Auth;
using SessionManager.Api.Models.Impersonation;
using SessionManager.Api.Services.Auth;
using SessionManager.Api.Services.Impersonation;
using SessionManager.Api.Services.Sessions;
using System.Security.Claims;
using System.Text;

namespace SessionManager.Api.Controllers;

[ApiController]
[Route("api/impersonate")]
public class ImpersonationController : ControllerBase
{
    private readonly IImpersonationService _impersonationService;
    private readonly ISessionService _sessionService;
    private readonly IAuthService _authService;
    private readonly ILogger<ImpersonationController> _logger;

    public ImpersonationController(
        IImpersonationService impersonationService,
        ISessionService sessionService,
        IAuthService authService,
        ILogger<ImpersonationController> logger)
    {
        _impersonationService = impersonationService;
        _sessionService = sessionService;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Start impersonating a user
    /// </summary>
    [HttpPost("{userId}")]
    public async Task<ActionResult<ApiResponse<StartImpersonationResponse>>> StartImpersonation(
        Guid userId,
        [FromBody] StartImpersonationRequest request)
    {
        // Get current session
        var sessionKey = GetSessionKey();
        if (string.IsNullOrEmpty(sessionKey))
        {
            return Unauthorized(new ApiResponse<StartImpersonationResponse>(false, default, default, "Not authenticated"));
        }

        // Get current user
        var currentUser = await _authService.GetCurrentUserAsync(sessionKey);
        if (currentUser == null)
        {
            return Unauthorized(new ApiResponse<StartImpersonationResponse>(false, default, default, "Invalid session"));
        }

        // Validate request
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new ApiResponse<StartImpersonationResponse>(false, default, default, "Reason is required"));
        }

        var duration = request.DurationMinutes ?? 30;
        if (duration < 1 || duration > 60)
        {
            return BadRequest(new ApiResponse<StartImpersonationResponse>(false, default, default, "Duration must be between 1 and 60 minutes"));
        }

        // Start impersonation
        var ipAddress = GetIpAddress();
        var userAgent = Request.Headers["User-Agent"].ToString();

        var result = await _impersonationService.StartAsync(
            currentUser.Id,
            sessionKey,
            userId,
            request.Reason,
            duration,
            ipAddress,
            userAgent);

        if (!result.Success)
        {
            return BadRequest(new ApiResponse<StartImpersonationResponse>(false, default, default, result.Error));
        }

        // Set the new session cookie
        Response.Cookies.Append(
            SessionManagerConstants.SessionCookieName,
            result.NewSessionKey!,
            new CookieOptions
            {
                Domain = SessionManagerConstants.CookieDomain,
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = result.ExpiresAt
            });

        var response = new StartImpersonationResponse(
            result.ImpersonationId!.Value,
            null, // Will be filled by service
            null, // Will be filled by service
            result.ExpiresAt!.Value,
            "Impersonation started successfully"
        );

        return Ok(new ApiResponse<StartImpersonationResponse>(true, response));
    }

    /// <summary>
    /// End current impersonation
    /// </summary>
    [HttpDelete]
    public async Task<ActionResult<ApiResponse<MessageResponse>>> EndImpersonation()
    {
        var sessionKey = GetSessionKey();
        if (string.IsNullOrEmpty(sessionKey))
        {
            return Unauthorized(new ApiResponse<MessageResponse>(false, default, default, "Not authenticated"));
        }

        var sessionData = await _sessionService.GetSessionAsync(sessionKey);
        if (sessionData == null || !sessionData.IsImpersonated)
        {
            return BadRequest(new ApiResponse<MessageResponse>(false, default, default, "Not currently impersonating"));
        }

        var originalSessionKey = await _impersonationService.EndAsync(sessionKey, "manual");
        if (originalSessionKey == null)
        {
            return BadRequest(new ApiResponse<MessageResponse>(false, default, default, "Failed to end impersonation"));
        }

        // Set the original session cookie back
        Response.Cookies.Append(
            SessionManagerConstants.SessionCookieName,
            originalSessionKey,
            new CookieOptions
            {
                Domain = SessionManagerConstants.CookieDomain,
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddHours(4)
            });

        return Ok(new ApiResponse<MessageResponse>(true, new MessageResponse("Impersonation ended")));
    }

    /// <summary>
    /// Get impersonation status
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<ImpersonationStatusResponse>>> GetStatus()
    {
        var sessionKey = GetSessionKey();
        if (string.IsNullOrEmpty(sessionKey))
        {
            return Ok(new ApiResponse<ImpersonationStatusResponse>(true, new ImpersonationStatusResponse(
                false, null, null, null, null, null, null)));
        }

        var status = await _impersonationService.GetStatusAsync(sessionKey);
        return Ok(new ApiResponse<ImpersonationStatusResponse>(true, status));
    }

    /// <summary>
    /// Get all active impersonation sessions (Super Admin only)
    /// </summary>
    [HttpGet("sessions")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ImpersonationSessionDto>>>> GetActiveSessions()
    {
        var sessionKey = GetSessionKey();
        if (string.IsNullOrEmpty(sessionKey))
        {
            return Unauthorized(new ApiResponse<IEnumerable<ImpersonationSessionDto>>(false, default, default, "Not authenticated"));
        }

        var currentUser = await _authService.GetCurrentUserAsync(sessionKey);
        if (currentUser == null || !currentUser.IsSuperAdmin)
        {
            return Forbid();
        }

        var sessions = await _impersonationService.GetActiveSessionsAsync();
        return Ok(new ApiResponse<IEnumerable<ImpersonationSessionDto>>(true, sessions));
    }

    /// <summary>
    /// Force-end an impersonation session (Super Admin only)
    /// </summary>
    [HttpDelete("sessions/{id}")]
    public async Task<ActionResult<ApiResponse<MessageResponse>>> ForceEndSession(Guid id)
    {
        var sessionKey = GetSessionKey();
        if (string.IsNullOrEmpty(sessionKey))
        {
            return Unauthorized(new ApiResponse<MessageResponse>(false, default, default, "Not authenticated"));
        }

        var currentUser = await _authService.GetCurrentUserAsync(sessionKey);
        if (currentUser == null || !currentUser.IsSuperAdmin)
        {
            return Forbid();
        }

        var success = await _impersonationService.ForceEndAsync(id, currentUser.Id, "admin_revoked");
        if (!success)
        {
            return NotFound(new ApiResponse<MessageResponse>(false, default, default, "Impersonation session not found"));
        }

        return Ok(new ApiResponse<MessageResponse>(true, new MessageResponse("Impersonation session ended")));
    }

    private string? GetSessionKey()
    {
        return Request.Cookies[SessionManagerConstants.SessionCookieName];
    }

    private string GetIpAddress()
    {
        // Check for forwarded IP (from proxy/load balancer)
        var forwardedIp = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedIp))
        {
            return forwardedIp.Split(',')[0].Trim();
        }

        // Check for real IP header
        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to remote IP
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
