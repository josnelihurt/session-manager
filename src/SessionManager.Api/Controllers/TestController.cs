using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SessionManager.Api.Data;
using SessionManager.Api.Entities;
using SessionManager.Api.Services.Sessions;
using SessionManager.Api.Services.Auth;

namespace SessionManager.Api.Controllers;

/// <summary>
/// Test controller for e2e testing
/// NOTE: Only enable in development/test environments
/// </summary>
[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    private readonly SessionManagerDbContext _dbContext;
    private readonly ISessionService _sessionService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TestController> _logger;

    public TestController(
        SessionManagerDbContext dbContext,
        ISessionService sessionService,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<TestController> logger)
    {
        _dbContext = dbContext;
        _sessionService = sessionService;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Check if test mode is enabled
    /// </summary>
    private bool IsTestModeEnabled()
    {
        return _configuration.GetValue<bool>("TestMode:Enabled", false);
    }

    /// <summary>
    /// Create a test user for e2e testing
    /// </summary>
    [HttpPost("create-user")]
    public async Task<ActionResult<dynamic>> CreateTestUser([FromBody] CreateTestUserRequest request)
    {
        if (!IsTestModeEnabled())
        {
            return Forbid("Test mode is not enabled");
        }

        // Check if user already exists
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Email);

        if (existingUser != null)
        {
            // Return existing user
            return Ok(new
            {
                id = existingUser.Id,
                username = existingUser.Username,
                email = existingUser.Email,
                isNew = false
            });
        }

        // Create new test user
        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = passwordHash,
            Provider = "local",
            IsSuperAdmin = request.IsSuperAdmin ?? false,
            CanImpersonate = request.CanImpersonate ?? false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created test user {Username} ({Email})", user.Username, user.Email);

        return Ok(new
        {
            id = user.Id,
            username = user.Username,
            email = user.Email,
            isNew = true
        });
    }

    /// <summary>
    /// Login and get session key (for e2e testing)
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<dynamic>> TestLogin([FromBody] TestLoginRequest request)
    {
        if (!IsTestModeEnabled())
        {
            return Forbid("Test mode is not enabled");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.Provider == "local");

        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
        {
            return Unauthorized(new { error = "Invalid credentials" });
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { error = "Invalid credentials" });
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        // Create session
        bool canImpersonate = user.IsSuperAdmin || user.CanImpersonate;
        var sessionKey = await _sessionService.CreateSessionAsync(
            user.Id,
            user.Username,
            user.Email,
            user.IsSuperAdmin,
            canImpersonate,
            "127.0.0.1",
            "e2e-test"
        );

        return Ok(new
        {
            sessionKey,
            user = new
            {
                id = user.Id,
                username = user.Username,
                email = user.Email,
                isSuperAdmin = user.IsSuperAdmin,
                canImpersonate = canImpersonate
            }
        });
    }

    /// <summary>
    /// Get OTP for a user (for e2e testing)
    /// </summary>
    [HttpGet("otp/{email}")]
    public async Task<ActionResult<dynamic>> GetOtpForUser(string email)
    {
        if (!IsTestModeEnabled())
        {
            return Forbid("Test mode is not enabled");
        }

        // This would require access to the OTP service/storage
        // For now, return not implemented
        return StatusCode(501, new { error = "OTP retrieval not implemented - requires database access to otp_records" });
    }

    /// <summary>
    /// Clean up test users
    /// </summary>
    [HttpDelete("cleanup")]
    public async Task<ActionResult<int>> CleanupTestUsers()
    {
        if (!IsTestModeEnabled())
        {
            return Forbid("Test mode is not enabled");
        }

        var testUsers = await _dbContext.Users
            .Where(u => u.Username.StartsWith("e2e-test-"))
            .ToListAsync();

        var count = testUsers.Count;
        if (count > 0)
        {
            _dbContext.Users.RemoveRange(testUsers);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Count} test users", count);
        }

        return Ok(count);
    }
}

public record CreateTestUserRequest(
    string Username,
    string Email,
    string Password,
    bool? IsSuperAdmin,
    bool? CanImpersonate
);

public record TestLoginRequest(
    string Username,
    string Password
);
