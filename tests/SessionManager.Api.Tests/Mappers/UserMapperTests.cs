using SessionManager.Api.Entities;
using SessionManager.Api.Mappers;
using SessionManager.Api.Models;
using FluentAssertions;
using Xunit;

namespace SessionManager.Api.Tests.Mappers;

public class UserMapperTests
{
    [Fact]
    public void ToDto_WithFullEntity_MapsAllProperties()
    {
        // Arrange
        var app = new Application { Id = Guid.NewGuid(), Name = "TestApp", Url = "test.com" };
        var role = new Role { Id = Guid.NewGuid(), Name = "Admin", Application = app };
        var userRole = new UserRole { RoleId = role.Id, Role = role };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            Provider = "local",
            IsActive = true,
            IsSuperAdmin = false,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            UserRoles = new List<UserRole> { userRole }
        };

        // Act
        var dto = UserMapper.ToDto(user);

        // Assert
        dto.Id.Should().Be(user.Id);
        dto.Username.Should().Be(user.Username);
        dto.Email.Should().Be(user.Email);
        dto.Provider.Should().Be(user.Provider);
        dto.IsActive.Should().Be(user.IsActive);
        dto.IsSuperAdmin.Should().Be(user.IsSuperAdmin);
        dto.CreatedAt.Should().Be(user.CreatedAt);
        dto.LastLoginAt.Should().Be(user.LastLoginAt);
        dto.Roles.Should().HaveCount(1);
        dto.Roles[0].RoleId.Should().Be(role.Id);
        dto.Roles[0].RoleName.Should().Be("Admin");
        dto.Roles[0].ApplicationName.Should().Be("TestApp");
        dto.Roles[0].ApplicationUrl.Should().Be("test.com");
    }

    [Fact]
    public void ToDto_WithoutRoles_ReturnsEmptyRolesArray()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            UserRoles = new List<UserRole>()
        };

        // Act
        var dto = UserMapper.ToDto(user);

        // Assert
        dto.Roles.Should().NotBeNull();
        dto.Roles.Should().BeEmpty();
    }

    [Fact]
    public void ToDto_WithCollection_MapsAllEntities()
    {
        // Arrange
        var app = new Application { Id = Guid.NewGuid(), Name = "TestApp", Url = "test.com" };
        var role = new Role { Id = Guid.NewGuid(), Name = "Admin", Application = app };
        var users = new List<User>
        {
            new() { Id = Guid.NewGuid(), Username = "user1", Email = "user1@example.com", UserRoles = new List<UserRole> { new UserRole { RoleId = role.Id, Role = role } } },
            new() { Id = Guid.NewGuid(), Username = "user2", Email = "user2@example.com", UserRoles = new List<UserRole> { new UserRole { RoleId = role.Id, Role = role } } }
        };

        // Act
        var dtos = UserMapper.ToDto(users);

        // Assert
        dtos.Should().HaveCount(2);
        dtos[0].Username.Should().Be("user1");
        dtos[1].Username.Should().Be("user2");
    }

    [Fact]
    public void ToDto_SuperAdmin_MapsCorrectly()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@example.com",
            IsSuperAdmin = true,
            IsActive = true,
            UserRoles = new List<UserRole>()
        };

        // Act
        var dto = UserMapper.ToDto(user);

        // Assert
        dto.IsSuperAdmin.Should().BeTrue();
    }
}
