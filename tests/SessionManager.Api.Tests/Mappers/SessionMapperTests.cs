using SessionManager.Api.Mappers;
using SessionManager.Api.Models;
using FluentAssertions;
using Xunit;

namespace SessionManager.Api.Tests.Mappers;

public class SessionMapperTests
{
    [Fact]
    public void ToDto_WithFullEntity_MapsAllProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddHours(2);
        var session = new SessionInfo(
            SessionId: "session-abc123",
            CookiePrefix: "session_manager",
            TtlMilliseconds: 7200000,
            ExpiresAt: expiresAt,
            FullKey: "session_manager:session-abc123",
            UserId: userId,
            Username: "testuser",
            Email: "test@example.com"
        );

        // Act
        var dto = SessionMapper.ToDto(session);

        // Assert
        dto.SessionId.Should().Be("session-abc123");
        dto.CookiePrefix.Should().Be("session_manager");
        dto.Ttl.Should().Be(7200000);
        dto.ExpiresAt.Should().Be(expiresAt.ToString("o"));
        dto.UserId.Should().Be(userId.ToString("N"));
        dto.Username.Should().Be("testuser");
        dto.Email.Should().Be("test@example.com");
        dto.Remaining.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ToDto_WithNullUserId_MapsCorrectly()
    {
        // Arrange
        var session = new SessionInfo(
            SessionId: "session-xyz",
            CookiePrefix: "session_manager",
            TtlMilliseconds: 3600000,
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            FullKey: "session_manager:session-xyz",
            UserId: null,
            Username: null,
            Email: null
        );

        // Act
        var dto = SessionMapper.ToDto(session);

        // Assert
        dto.UserId.Should().BeNull();
        dto.Username.Should().BeNull();
        dto.Email.Should().BeNull();
    }

    [Fact]
    public void ToDto_WithCollection_MapsAllEntities()
    {
        // Arrange
        var sessions = new List<SessionInfo>
        {
            new("session1", "session_manager", 7200000, DateTime.UtcNow.AddHours(2), "key1", Guid.NewGuid(), "user1", "user1@example.com"),
            new("session2", "session_manager", 3600000, DateTime.UtcNow.AddHours(1), "key2", Guid.NewGuid(), "user2", "user2@example.com")
        };

        // Act
        var dtos = SessionMapper.ToDto(sessions);

        // Assert
        dtos.Should().HaveCount(2);
        dtos[0].Username.Should().Be("user1");
        dtos[1].Username.Should().Be("user2");
    }

    [Fact]
    public void ToDto_FormatRemainingTime_Days()
    {
        // Arrange
        var session = new SessionInfo(
            SessionId: "session-long",
            CookiePrefix: "session_manager",
            TtlMilliseconds: 90000000, // 25 hours
            ExpiresAt: DateTime.UtcNow.AddHours(25),
            FullKey: "key",
            UserId: Guid.NewGuid(),
            Username: "user",
            Email: "user@example.com"
        );

        // Act
        var dto = SessionMapper.ToDto(session);

        // Assert
        dto.Remaining.Should().Contain("d");
        dto.Remaining.Should().Contain("h");
    }

    [Fact]
    public void ToDto_FormatRemainingTime_Hours()
    {
        // Arrange
        var session = new SessionInfo(
            SessionId: "session-medium",
            CookiePrefix: "session_manager",
            TtlMilliseconds: 5400000, // 90 minutes
            ExpiresAt: DateTime.UtcNow.AddMinutes(90),
            FullKey: "key",
            UserId: Guid.NewGuid(),
            Username: "user",
            Email: "user@example.com"
        );

        // Act
        var dto = SessionMapper.ToDto(session);

        // Assert
        dto.Remaining.Should().Contain("h");
        dto.Remaining.Should().Contain("m");
    }

    [Fact]
    public void ToDto_FormatRemainingTime_Minutes()
    {
        // Arrange
        var session = new SessionInfo(
            SessionId: "session-short",
            CookiePrefix: "session_manager",
            TtlMilliseconds: 120000, // 2 minutes
            ExpiresAt: DateTime.UtcNow.AddMinutes(2),
            FullKey: "key",
            UserId: Guid.NewGuid(),
            Username: "user",
            Email: "user@example.com"
        );

        // Act
        var dto = SessionMapper.ToDto(session);

        // Assert
        dto.Remaining.Should().Contain("m");
        dto.Remaining.Should().Contain("s");
    }

    [Fact]
    public void ToDto_FormatRemainingTime_Expired()
    {
        // Arrange
        var session = new SessionInfo(
            SessionId: "session-expired",
            CookiePrefix: "session_manager",
            TtlMilliseconds: 0,
            ExpiresAt: null,
            FullKey: "key",
            UserId: Guid.NewGuid(),
            Username: "user",
            Email: "user@example.com"
        );

        // Act
        var dto = SessionMapper.ToDto(session);

        // Assert
        dto.Remaining.Should().Be("Expired");
    }

    [Fact]
    public void ToDto_FormatRemainingTime_Negative()
    {
        // Arrange
        var session = new SessionInfo(
            SessionId: "session-negative",
            CookiePrefix: "session_manager",
            TtlMilliseconds: -1000,
            ExpiresAt: null,
            FullKey: "key",
            UserId: Guid.NewGuid(),
            Username: "user",
            Email: "user@example.com"
        );

        // Act
        var dto = SessionMapper.ToDto(session);

        // Assert
        dto.Remaining.Should().Be("Expired");
    }
}
