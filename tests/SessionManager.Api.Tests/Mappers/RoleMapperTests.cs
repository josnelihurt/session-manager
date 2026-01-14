using System.Text.Json;
using SessionManager.Api.Entities;
using SessionManager.Api.Mappers;
using SessionManager.Api.Models;
using FluentAssertions;
using Xunit;

namespace SessionManager.Api.Tests.Mappers;

public class RoleMapperTests
{
    [Fact]
    public void ToDto_WithFullEntity_MapsAllProperties()
    {
        // Arrange
        var permissions = new Dictionary<string, bool>
        {
            { "read", true },
            { "write", true },
            { "delete", false }
        };
        var role = new Role
        {
            Id = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            Name = "Editor",
            PermissionsJson = JsonSerializer.Serialize(permissions)
        };

        // Act
        var dto = RoleMapper.ToDto(role);

        // Assert
        dto.Id.Should().Be(role.Id);
        dto.ApplicationId.Should().Be(role.ApplicationId);
        dto.Name.Should().Be(role.Name);
        dto.Permissions.Should().HaveCount(3);
        dto.Permissions["read"].Should().BeTrue();
        dto.Permissions["write"].Should().BeTrue();
        dto.Permissions["delete"].Should().BeFalse();
    }

    [Fact]
    public void ToDto_WithEmptyPermissions_ReturnsEmptyDictionary()
    {
        // Arrange
        var role = new Role
        {
            Id = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            Name = "Viewer",
            PermissionsJson = ""
        };

        // Act
        var dto = RoleMapper.ToDto(role);

        // Assert
        dto.Permissions.Should().NotBeNull();
        dto.Permissions.Should().BeEmpty();
    }

    [Fact]
    public void ToDto_WithNullPermissions_ReturnsEmptyDictionary()
    {
        // Arrange
        var role = new Role
        {
            Id = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            Name = "Viewer",
            PermissionsJson = null
        };

        // Act
        var dto = RoleMapper.ToDto(role);

        // Assert
        dto.Permissions.Should().NotBeNull();
        dto.Permissions.Should().BeEmpty();
    }

    [Fact]
    public void ToDto_WithCollection_MapsAllEntities()
    {
        // Arrange
        var roles = new List<Role>
        {
            new() { Id = Guid.NewGuid(), Name = "Admin", ApplicationId = Guid.NewGuid(), PermissionsJson = "{}" },
            new() { Id = Guid.NewGuid(), Name = "User", ApplicationId = Guid.NewGuid(), PermissionsJson = "{}" }
        };

        // Act
        var dtos = RoleMapper.ToDto(roles);

        // Assert
        dtos.Should().HaveCount(2);
        dtos[0].Name.Should().Be("Admin");
        dtos[1].Name.Should().Be("User");
    }

    [Fact]
    public void ToDto_WithInvalidJson_ReturnsEmptyDictionary()
    {
        // Arrange
        var role = new Role
        {
            Id = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            Name = "TestRole",
            PermissionsJson = "invalid json"
        };

        // Act
        var dto = RoleMapper.ToDto(role);

        // Assert
        dto.Permissions.Should().NotBeNull();
        dto.Permissions.Should().BeEmpty();
    }
}
