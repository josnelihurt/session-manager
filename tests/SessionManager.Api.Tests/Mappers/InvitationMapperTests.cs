using SessionManager.Api.Entities;
using SessionManager.Api.Mappers;
using SessionManager.Api.Models;
using FluentAssertions;
using Xunit;

namespace SessionManager.Api.Tests.Mappers;

public class InvitationMapperTests
{
    [Fact]
    public void ToDto_WithFullEntity_MapsAllProperties()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            Token = "test-token-abc123",
            Email = "invite@example.com",
            Provider = "local",
            PreAssignedRoles = new Guid[] { roleId },
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            UsedAt = null
        };

        // Act
        var dto = InvitationMapper.ToDto(invitation);

        // Assert
        dto.Id.Should().Be(invitation.Id);
        dto.Token.Should().Be(invitation.Token);
        dto.Email.Should().Be(invitation.Email);
        dto.Provider.Should().Be(invitation.Provider);
        dto.PreAssignedRoles.Should().HaveCount(1);
        dto.PreAssignedRoles[0].Should().Be(roleId.ToString());
        dto.CreatedAt.Should().Be(invitation.CreatedAt);
        dto.ExpiresAt.Should().Be(invitation.ExpiresAt);
        dto.IsUsed.Should().BeFalse();
        dto.InviteUrl.Should().Be("https://session-manager.lab.josnelihurt.me/register?token=test-token-abc123");
    }

    [Fact]
    public void ToDto_WhenUsed_MapsUsedAtAndIsUsed()
    {
        // Arrange
        var usedAt = DateTime.UtcNow;
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            Token = "used-token",
            Email = "used@example.com",
            Provider = "google",
            PreAssignedRoles = null,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(6),
            UsedAt = usedAt
        };

        // Act
        var dto = InvitationMapper.ToDto(invitation);

        // Assert
        dto.IsUsed.Should().BeTrue();
        dto.UsedAt.Should().Be(usedAt);
    }

    [Fact]
    public void ToDto_WithoutPreAssignedRoles_ReturnsEmptyArray()
    {
        // Arrange
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            Token = "no-roles-token",
            Email = "noroles@example.com",
            Provider = "auth0",
            PreAssignedRoles = null,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            UsedAt = null
        };

        // Act
        var dto = InvitationMapper.ToDto(invitation);

        // Assert
        dto.PreAssignedRoles.Should().NotBeNull();
        dto.PreAssignedRoles.Should().BeEmpty();
    }

    [Fact]
    public void ToDto_WithCollection_MapsAllEntities()
    {
        // Arrange
        var invitations = new List<Invitation>
        {
            new() { Id = Guid.NewGuid(), Token = "token1", Email = "email1@example.com", Provider = "local", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7) },
            new() { Id = Guid.NewGuid(), Token = "token2", Email = "email2@example.com", Provider = "google", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7) }
        };

        // Act
        var dtos = InvitationMapper.ToDto(invitations);

        // Assert
        dtos.Should().HaveCount(2);
        dtos[0].Email.Should().Be("email1@example.com");
        dtos[1].Email.Should().Be("email2@example.com");
    }

    [Fact]
    public void ToDto_GeneratesCorrectInviteUrl()
    {
        // Arrange
        var token = "special-token-xyz";
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            Token = token,
            Email = "test@example.com",
            Provider = "local",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        // Act
        var dto = InvitationMapper.ToDto(invitation);

        // Assert
        dto.InviteUrl.Should().Be($"https://session-manager.lab.josnelihurt.me/register?token={token}");
    }
}
