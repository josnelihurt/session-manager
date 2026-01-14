using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Mappers;
using SessionManager.Api.Models;
using SessionManager.Api.Models.Auth;
using SessionManager.Api.Services.Sessions;
using SessionManager.Api.Services.Auth;

namespace SessionManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly IAuthService _authService;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        ISessionService sessionService,
        IAuthService authService,
        IOptions<AuthOptions> authOptions,
        ILogger<SessionsController> logger)
    {
        _sessionService = sessionService;
        _authService = authService;
        _authOptions = authOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Get all OAuth2 proxy sessions from Redis
    /// Non-super-admin users only see their own sessions
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SessionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SessionsResponse>> GetSessions()
    {
        try
        {
            // Get current user from session
            var sessionKey = Request.Cookies[_authOptions.CookieName];
            UserInfo? currentUser = null;

            if (!string.IsNullOrEmpty(sessionKey))
            {
                currentUser = await _authService.GetCurrentUserAsync(sessionKey);
            }

            // Get all sessions
            var allSessions = await _sessionService.GetAllSessionsAsync();

            // Filter sessions: non-super-admins only see their own sessions
            IEnumerable<SessionInfo> sessions;
            if (currentUser != null && currentUser.IsSuperAdmin)
            {
                // Super admin sees all sessions
                sessions = allSessions;
            }
            else if (currentUser != null)
            {
                // Regular user sees only their own sessions
                sessions = allSessions.Where(s => s.UserId == currentUser.Id);
            }
            else
            {
                // Not authenticated - return empty list
                sessions = Enumerable.Empty<SessionInfo>();
            }

            var sessionDtos = SessionMapper.ToDto(sessions);

            return Ok(new SessionsResponse(true, sessionDtos, sessions.Count()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sessions");
            return StatusCode(500, new ApiResponse<object>(false, Error: "Failed to retrieve sessions"));
        }
    }

    /// <summary>
    /// Delete a specific session by key
    /// </summary>
    [HttpDelete("{fullKey}")]
    [ProducesResponseType(typeof(DeleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DeleteResponse>> DeleteSession(string fullKey)
    {
        try
        {
            fullKey = Uri.UnescapeDataString(fullKey);

            var result = await _sessionService.DeleteSessionAsync(fullKey);
            var message = result ? "Session deleted successfully" : "Session not found";

            return Ok(new DeleteResponse(result, message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting session {Key}", fullKey);
            return StatusCode(500, new ApiResponse<object>(false, Error: "Failed to delete session"));
        }
    }

    /// <summary>
    /// Delete all OAuth2 proxy sessions
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(typeof(DeleteAllResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DeleteAllResponse>> DeleteAllSessions()
    {
        try
        {
            var count = await _sessionService.DeleteAllSessionsAsync();
            return Ok(new DeleteAllResponse(true, $"Deleted {count} session(s)", count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all sessions");
            return StatusCode(500, new ApiResponse<object>(false, Error: "Failed to delete sessions"));
        }
    }
}
