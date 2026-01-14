using SessionManager.Api.Entities;
using SessionManager.Api.Mappers;
using SessionManager.Api.Models;
using FluentAssertions;
using Xunit;

namespace SessionManager.Api.Tests.Mappers;

public class ApplicationMapperTests
{
    [Fact]
    public void ToDto_WithFullEntity_MapsAllProperties()
    {
        // Arrange
        var app = new Application
        {
            Id = Guid.NewGuid(),
            Name = "TestApp",
            Url = "test.com",
            Description = "Test Description",
            IsActive = true,
            Roles = new List<Role>
            {
                new() { Id = Guid.NewGuid(), Name = "Admin", ApplicationId = Guid.NewGuid() },
                new() { Id = Guid.NewGuid(), Name = "User", ApplicationId = Guid.NewGuid() }
            }
        };

        // Act
        var dto = ApplicationMapper.ToDto(app);

        // Assert
        dto.Id.Should().Be(app.Id);
        dto.Name.Should().Be(app.Name);
        dto.Url.Should().Be(app.Url);
        dto.Description.Should().Be(app.Description);
        dto.IsActive.Should().Be(app.IsActive);
        dto.Roles.Should().HaveCount(2);
        dto.Roles[0].Name.Should().Be("Admin");
        dto.Roles[1].Name.Should().Be("User");
    }

    [Fact]
    public void ToDto_WithoutRoles_ReturnsEmptyRolesArray()
    {
        // Arrange
        var app = new Application
        {
            Id = Guid.NewGuid(),
            Name = "TestApp",
            Url = "test.com",
            Roles = new List<Role>()
        };

        // Act
        var dto = ApplicationMapper.ToDto(app);

        // Assert
        dto.Roles.Should().NotBeNull();
        dto.Roles.Should().BeEmpty();
    }

    [Fact]
    public void ToDto_WithCollection_MapsAllEntities()
    {
        // Arrange
        var apps = new List<Application>
        {
            new() { Id = Guid.NewGuid(), Name = "App1", Url = "app1.com", Roles = new List<Role>() },
            new() { Id = Guid.NewGuid(), Name = "App2", Url = "app2.com", Roles = new List<Role>() }
        };

        // Act
        var dtos = ApplicationMapper.ToDto(apps);

        // Assert
        dtos.Should().HaveCount(2);
        dtos[0].Name.Should().Be("App1");
        dtos[1].Name.Should().Be("App2");
    }

    [Fact]
    public void ToDto_InactiveApplication_MapsCorrectly()
    {
        // Arrange
        var app = new Application
        {
            Id = Guid.NewGuid(),
            Name = "InactiveApp",
            Url = "inactive.com",
            IsActive = false,
            Roles = new List<Role>()
        };

        // Act
        var dto = ApplicationMapper.ToDto(app);

        // Assert
        dto.IsActive.Should().BeFalse();
    }
}
