using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SessionManager.Api.Configuration;
using SessionManager.Api.Controllers;
using SessionManager.Api.Models;
using SessionManager.Api.Models.Auth;
using SessionManager.Api.Services;
using Xunit;

namespace SessionManager.Api.Tests;

public class SessionsControllerTests
{
    private readonly Mock<ISessionService> _sessionServiceMock;
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<ILogger<SessionsController>> _loggerMock;
    private readonly Mock<IOptions<AuthOptions>> _authOptionsMock;
    private readonly SessionsController _controller;

    public SessionsControllerTests()
    {
        _sessionServiceMock = new Mock<ISessionService>();
        _authServiceMock = new Mock<IAuthService>();
        _loggerMock = new Mock<ILogger<SessionsController>>();
        _authOptionsMock = new Mock<IOptions<AuthOptions>>();

        var authOptions = new AuthOptions
        {
            CookieName = "_session_manager",
            CookieDomain = ".lab.josnelihurt.me",
            SessionLifetimeHours = 24
        };
        _authOptionsMock.Setup(x => x.Value).Returns(authOptions);

        _controller = new SessionsController(_sessionServiceMock.Object, _authServiceMock.Object, _authOptionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetSessions_ReturnsOkWithSessions_WhenSessionsExist()
    {
        // Arrange
        var testUserId = Guid.NewGuid();
        var sessions = new List<SessionInfo>
        {
            new("abc123", "_oauth2_proxy_redis", 3600000, DateTime.UtcNow.AddHours(1), "_oauth2_proxy_redis-abc123", testUserId, "testuser", "test@example.com"),
            new("def456", "_oauth2_proxy_redis", 7200000, DateTime.UtcNow.AddHours(2), "_oauth2_proxy_redis-def456", testUserId, "testuser", "test@example.com")
        };
        _sessionServiceMock.Setup(s => s.GetAllSessionsAsync()).ReturnsAsync(sessions);

        // Mock super admin user
        var userInfo = new UserInfo(testUserId, "testuser", "test@example.com", true, "local");
        _authServiceMock.Setup(s => s.GetCurrentUserAsync(It.IsAny<string>())).ReturnsAsync(userInfo);

        // Set up controller context with cookies
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Cookie"] = "_session_manager=fake-session-key";
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = await _controller.GetSessions();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SessionsResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Count.Should().Be(2);
        response.Data.Should().HaveCount(2);
        response.Data.First().SessionId.Should().Be("abc123");
    }

    [Fact]
    public async Task GetSessions_ReturnsEmptyList_WhenNoSessionsExist()
    {
        // Arrange
        _sessionServiceMock.Setup(s => s.GetAllSessionsAsync()).ReturnsAsync(new List<SessionInfo>());

        // Mock super admin user
        var testUserId = Guid.NewGuid();
        var userInfo = new UserInfo(testUserId, "testuser", "test@example.com", true, "local");
        _authServiceMock.Setup(s => s.GetCurrentUserAsync(It.IsAny<string>())).ReturnsAsync(userInfo);

        // Set up controller context with cookies
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Cookie"] = "_session_manager=fake-session-key";
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = await _controller.GetSessions();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SessionsResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Count.Should().Be(0);
        response.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSessions_Returns500_WhenServiceThrows()
    {
        // Arrange
        _sessionServiceMock.Setup(s => s.GetAllSessionsAsync())
            .ThrowsAsync(new Exception("Redis connection failed"));

        // Act
        var result = await _controller.GetSessions();

        // Assert
        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);

        var response = statusResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Error.Should().Be("Failed to retrieve sessions");
    }

    [Fact]
    public async Task DeleteSession_ReturnsSuccess_WhenSessionDeleted()
    {
        // Arrange
        var fullKey = "_oauth2_proxy_redis-abc123";
        _sessionServiceMock.Setup(s => s.DeleteSessionAsync(fullKey)).ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteSession(fullKey);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DeleteResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Message.Should().Be("Session deleted successfully");
    }

    [Fact]
    public async Task DeleteSession_ReturnsNotFound_WhenSessionDoesNotExist()
    {
        // Arrange
        var fullKey = "_oauth2_proxy_redis-nonexistent";
        _sessionServiceMock.Setup(s => s.DeleteSessionAsync(fullKey)).ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteSession(fullKey);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DeleteResponse>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Be("Session not found");
    }

    [Fact]
    public async Task DeleteSession_DecodesUrlEncodedKey()
    {
        // Arrange
        var encodedKey = "_oauth2_proxy_redis%2Dabc123";
        var decodedKey = "_oauth2_proxy_redis-abc123";
        _sessionServiceMock.Setup(s => s.DeleteSessionAsync(decodedKey)).ReturnsAsync(true);

        // Act
        await _controller.DeleteSession(encodedKey);

        // Assert
        _sessionServiceMock.Verify(s => s.DeleteSessionAsync(decodedKey), Times.Once);
    }

    [Fact]
    public async Task DeleteAllSessions_ReturnsCount_WhenSessionsDeleted()
    {
        // Arrange
        _sessionServiceMock.Setup(s => s.DeleteAllSessionsAsync()).ReturnsAsync(5);

        // Act
        var result = await _controller.DeleteAllSessions();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DeleteAllResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Count.Should().Be(5);
        response.Message.Should().Be("Deleted 5 session(s)");
    }
}
