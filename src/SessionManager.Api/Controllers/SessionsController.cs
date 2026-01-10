using Microsoft.AspNetCore.Mvc;
using SessionManager.Api.Models;
using SessionManager.Api.Services;

namespace SessionManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        ISessionService sessionService,
        ILogger<SessionsController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Get all OAuth2 proxy sessions from Redis
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SessionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SessionsResponse>> GetSessions()
    {
        try
        {
            var sessions = await _sessionService.GetAllSessionsAsync();
            var sessionDtos = sessions.Select(s => new SessionDto(
                s.SessionId,
                s.CookiePrefix,
                s.TtlMilliseconds,
                s.ExpiresAt?.ToString("o"),
                FormatRemainingTime(s.TtlMilliseconds),
                s.FullKey
            ));

            return Ok(new SessionsResponse(true, sessionDtos, sessions.Count));
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

    private static string FormatRemainingTime(long ttlMs)
    {
        if (ttlMs < 0)
            return "Expired";

        var timeSpan = TimeSpan.FromMilliseconds(ttlMs);

        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
        if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        return $"{timeSpan.Seconds}s";
    }
}
